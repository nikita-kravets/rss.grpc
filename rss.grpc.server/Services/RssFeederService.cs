using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace rss.grpc.server
{
    using Models;
    using System.Globalization;

    public class RssFeederService : RssFeeder.RssFeederBase
    {
        private readonly ILogger<RssFeederService> _logger;
        private readonly IRssManager _rssManager;
        private static readonly SemaphoreLocker _locker = new SemaphoreLocker();

        private static bool isReaderActive = false;
        static Dictionary<int, int> lastLoadedRss;

        private static string[] feeds =  {
            "https://www.theguardian.com/uk/rss",
            "https://lenta.ru/rss",
            "https://rss.nytimes.com/services/xml/rss/nyt/World.xml"
        };
        private void LogInformationSafe(string message)
        {
            lock ("logger")
            {
                _logger.LogInformation(message);
            }
        }
        private void UpdateLastLoadedSafe(int clientId, int value)
        {
            lock ("last")
            {
                lastLoadedRss[clientId] = value;
            }
        }
        private async void ReadFeedsBackground()
        {
            await Task.Run(async () =>
            {
                try
                {
                    lock ("activation")
                    {
                        if (isReaderActive)
                        {
                            return;
                        }
                        isReaderActive = true;
                    }

                    LogInformationSafe("Background RSS reader: Start reading RSS sources");

                    foreach (var feed in feeds)
                    {
                        WebClient webClient = new WebClient();
                        var xml = webClient.DownloadString(feed);
                        XDocument doc = XDocument.Parse(xml);
                        Dictionary<string, (RepeatedField<string>, string)> send =
                            new Dictionary<string, (RepeatedField<string>, string)>();

                        var items = from xe in doc
                            .Element("rss")
                            .Element("channel")
                            .Elements("item")
                                    where xe
                                    .Elements("category")
                                    .Count() > 0
                                    select xe;

                        foreach (var item in items)
                        {
                            string k = item.Element("guid").Value;

                            //lock db usage
                            await _locker.LockAsync(async () =>
                            {
                                if (!await _rssManager.HasRss(k))
                                {
                                    string ds = item.Element("pubDate").Value;
                                    DateTime pubDate;
                                    if (!DateTime.TryParse(ds,
                                                  CultureInfo.InvariantCulture,
                                                  DateTimeStyles.None,
                                                  out pubDate))
                                    {
                                        pubDate = DateTime.Now;
                                    }

                                    var tags = item.Elements("category");
                                    var rssId = await _rssManager.CreateRss(k, item.ToString(), pubDate);

                                    foreach (var tag in tags)
                                    {
                                        await _rssManager.RssAddTag(rssId, tag.Value);
                                    }
                                }
                            });
                        }
                    }
                    Thread.Sleep(1000 * 300);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Background RSS reader: " + ex.ToString());
                    Thread.Sleep(10000);
                }
            });

            lock ("activation")
            {
                isReaderActive = false;
            }

            //restart
            ReadFeedsBackground();
        }

        public RssFeederService(ILogger<RssFeederService> logger,
            IRssManager rssManager)
        {
            _logger = logger;
            _rssManager = rssManager;

            LogInformationSafe("Service instance started");

            //init static array which stores last rss id loaded for client
            if (lastLoadedRss == null)
            {
                lastLoadedRss = new Dictionary<int, int>();
            }

            //when some client is connected start update feeds if not already updating
            if (!isReaderActive)
            {
                ReadFeedsBackground();
            }
        }

        override public async Task<ClientInfo> NewClient(NewClientRequest request, ServerCallContext context)
        {
            int id = await _locker.LockAsync(async () =>
                await _rssManager.AddClient(request.Name, request.Email));
            LogInformationSafe("New client added " + id);
            return new ClientInfo() { ClientId = id };
        }

        override public async Task<SubscriptionResponse> Subscribe(SubscriprionRequest request,
            ServerCallContext context)
        {
            bool res = await _locker.LockAsync(async () =>
                await _rssManager.Subscribe(request.ClientId, request.Tag));
            LogInformationSafe("Client " + request.ClientId + " subscribed to \"" + request.Tag + "\"");
            //force refresh
            UpdateLastLoadedSafe(request.ClientId, 0);
            return new SubscriptionResponse { Result = res };
        }

        override public async Task<SubscriptionResponse> Unsubscribe(SubscriprionRequest request,
            ServerCallContext context)
        {
            bool res = await _locker.LockAsync(async () =>
                await _rssManager.Unsubscribe(request.ClientId, request.Tag));
            LogInformationSafe("Client " + request.ClientId + " unsubscribed from \"" + request.Tag + "\"");
            //force refresh
            UpdateLastLoadedSafe(request.ClientId, 0);
            return new SubscriptionResponse() { Result = res };
        }

        override public async Task ReadFeeds(ClientInfo request, IServerStreamWriter<FeedInfo> responseStream,
            ServerCallContext context)
        {
            try
            {
                var client = await _locker.LockAsync(async () =>
                    await _rssManager.GetClient(request.ClientId));

                if (client != null)
                {
                    //reset when client connected, save just for online time
                    //0 because client does not store feeds now
                    //force refresh
                    UpdateLastLoadedSafe(request.ClientId, 0);

                    //continuous wait for some new aggregation
                    while (true)
                    {
                        //sorted by date descending
                        var rsses = await _locker.LockAsync(async () =>
                            await _rssManager.RssForClient(client.ClientId,
                            lastLoadedRss.Keys.Contains(client.ClientId) ? lastLoadedRss[client.ClientId] : 0,
                            DateTime.Now.AddDays(-2))
                        );

                        if (rsses.Count > 0)
                        {
                            LogInformationSafe("Transferring " + rsses.Count + " feeds to client " + client.ClientId);

                            UpdateLastLoadedSafe(request.ClientId, rsses.Max(r => r.RssId));

                            foreach (var rss in rsses)
                            {
                                var fi = new FeedInfo() { FeedXml = rss.FeedXml };
                                foreach (var tag in rss.RssToTags)
                                {
                                    fi.Tags.Add(tag.Tag.TagName);
                                }
                                Thread.Sleep(50);
                                
                                await responseStream.WriteAsync(fi);
                                
                            }
                            
                            LogInformationSafe("All feeds have been transferred to " + client.ClientId);
                        }
                        //sleep 20 sec
                        Thread.Sleep(1000 * 20);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Client RSS sender: " + ex.ToString());
                
            }
            context.Status = Status.DefaultCancelled;
        }
    }
}