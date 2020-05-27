using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.Specialized;
using System.Xml.Linq;
using rss.grpc.server;

namespace rss.grpc.client
{
    using Properties;
    using Grpc.Net.Client;
    using System.Threading;
    using System.Net.Http;
    using Grpc.Core;
    using System.ComponentModel;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ITagRebinder, INotifyPropertyChanged
    {
        private RssFeeder.RssFeederClient client;
        Dictionary<string, XDocument> loadedFeeds;
        bool needUpdate = false;
        bool closing = false;

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string name) {
            if (this.PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public StringCollection Tags { get => Settings.Default.Tags; }
        public string LoadedCount { get => loadedFeeds != null ? loadedFeeds.Count.ToString() : "0"; }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            Settings.Default.TagRebinder = this;
            this.Closing += MainWindow_Closing;

            if (Settings.Default.Tags == null)
            {
                Settings.Default.Tags = new StringCollection();
            }
            loadedFeeds = new Dictionary<string, XDocument>();
            

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
        }

        async void PrepareConnection()
        {
            try
            {
                //Https with TLS not supported on Windows 7 and Mac
                //see https://docs.microsoft.com/ru-ru/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.1
#if USE_HTTPS
                
                var httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                var httpClient = new HttpClient(httpClientHandler);
                var channel = GrpcChannel.ForAddress("https://localhost:5001",
                    new GrpcChannelOptions { HttpClient = httpClient });
#else
                AppContext.SetSwitch(
                    "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                var channel = GrpcChannel.ForAddress("http://localhost:5000");
                
#endif
                client = new RssFeeder.RssFeederClient(channel);

                //on first startup show optional name email dialog
                if (Settings.Default.ClientId == 0)
                {
                    NameEmailWindow wnd = new NameEmailWindow();
                    wnd.Owner = this;

                    string name = "";
                    string email = "";

                    if (wnd.ShowDialog() == true)
                    {
                        name = wnd.ClientName;
                        email = wnd.Email;
                    }

                    var result = await client.NewClientAsync(new NewClientRequest() { Email = email, Name = name });
                    Settings.Default.ClientId = result.ClientId;
                    Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error has been occurred! " + ex.Message + "\n Application will be closed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        async void MonitorUpdateRss()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    if (needUpdate)
                    {
                        lock ("feeds")
                        {
                            var all = loadedFeeds.Values
                            .Select(xd => new
                            {
                                Html = xd.Root.Element("description").Value.Replace("<a ", "<a target='_blank' "),
                                Date = DateTime.Parse(xd.Root.Element("pubDate").Value),
                                Categories = xd.Root.Elements("category")
                                    .Select(c => Tags.Contains(c.Value) ? "<b><u>" + c.Value + "</u></b>" : c.Value)
                                    .Aggregate((c1, c2) => c1 + ", " + c2)
                            })
                            .Where(r => r.Html.Trim() != "")
                            .OrderByDescending(r => r.Date)
                            .Select((r, i) => "<h3>Feed #" + (i + 1) + " / " + r.Date.ToString("dd.MM.yyyy HH:mm") + "</h3>" +
                            "<div style='padding: 5px; border-bottom: 1px solid #aaa; margin-top: 0px; margin-bottom: 5px;'>" +
                            r.Html +
                            "<div style='margin-top: 10px; margin-bottom: 3px; font-size: 12px; color: #777; text-align: right;'>Categories: " + r.Categories + "</div>" +
                            "</div>"
                             )
                            .Aggregate((r1, r2) => { return r1 + r2; });

                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                lock ("browser")
                                {
                                    try
                                    {
                                        rssBrowser.NavigateToString("<html><head><meta charset=\"utf-8\"></head><body>" + all + "</body></html>");
                                    }
                                    catch { 
                                    
                                    }
                                    RaisePropertyChanged("LoadedCount");
                                }
                            }));
                            needUpdate = false;
                        }
                    }
                    Thread.Sleep(3000);
                }
            });
        }
        async void StartListenFeeds()
        {
            if (Settings.Default.ClientId == 0) {
                await Task.Delay(2000);
                StartListenFeeds();
                return;
            }

            var response = client.ReadFeeds(new ClientInfo() { ClientId = Settings.Default.ClientId });
            var cancellationToken = new CancellationToken();

            try
            {
                while (await response.ResponseStream.MoveNext(cancellationToken))
                {
                    var result = response.ResponseStream.Current;
                    XDocument doc = XDocument.Parse(result.FeedXml);

                    var guid = doc.Root.Element("guid").Value;

                    if (!loadedFeeds.ContainsKey(guid))
                    {
                        lock ("feeds")
                        {
                            loadedFeeds[guid] = doc;
                            needUpdate = true;
                        }
                    }
                }
            }
            catch (RpcException ex) {
                //on cancel - retry, otherwise app will shutdown
                if (ex.Status.StatusCode != StatusCode.Cancelled) {
                    throw ex;
                }
            }

            //retry
            if (!closing) {
                await Task.Delay(3000);
                StartListenFeeds();
            }
        }

        //don't want to use an ObservableCollection
        public void RebindTags()
        {
            int selIndex = tagList.SelectedIndex;
            tagList.ItemsSource = null;
            tagList.ItemsSource = Tags;

            if (selIndex != -1 && tagList.Items.Count > selIndex)
            {
                tagList.SelectedIndex = selIndex;
            }
        }

        private void ServerErrorUpdateTag()
        {
            MessageBox.Show("Server cannot update tag!", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void addTagButton_Click(object sender, RoutedEventArgs e)
        {
            TagEditWindow wnd = new TagEditWindow();
            wnd.Owner = this;

            if (wnd.ShowDialog() == true)
            {
                if (!Settings.Default.Tags.Contains(wnd.TagText))
                {
                    var response = await client.SubscribeAsync(new SubscriprionRequest() 
                        { ClientId = Settings.Default.ClientId, Tag = wnd.TagText });
                    if (response.Result)
                    {
                        Settings.Default.Tags.Add(wnd.TagText);
                        Settings.Default.Save();
                    }
                    else
                    {
                        ServerErrorUpdateTag();
                    }
                }
                else
                {
                    MessageBox.Show("Tag already exists!", "Warning", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void removeTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (tagList.SelectedIndex != -1)
            {
                if (MessageBox.Show("Proceed removing tag?", "Confirmation", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    string tag = Settings.Default.Tags[tagList.SelectedIndex];
                    var response = await client.UnsubscribeAsync(
                        new SubscriprionRequest()
                        {
                            ClientId = Settings.Default.ClientId,
                            Tag = tag
                        });

                    if (response.Result)
                    {
                        Settings.Default.Tags.RemoveAt(tagList.SelectedIndex);
                        Settings.Default.Save();
                        //remove feeds, containing removing tag
                        lock ("feeds")
                        {
                            //don't check for existent tags beacause list will be refreshed from server
                            //according to tags left
                            var all = loadedFeeds
                            .Where(kv => kv.Value.Root.Elements("category")
                            .Where(c => c.Value == tag).Count() > 0)
                            .Select(kv => kv.Key)
                            .ToList();
                            all.ForEach(k => loadedFeeds.Remove(k));
                        }
                    }
                    else
                    {
                        ServerErrorUpdateTag();
                    }
                }
            }
        }

        private async void editTagButton_Click(object sender, RoutedEventArgs e)
        {
            int selIndex = tagList.SelectedIndex;

            if (selIndex != -1)
            {
                TagEditWindow wnd = new TagEditWindow();
                wnd.Owner = this;
                string oldTag = tagList.SelectedItem.ToString();
                wnd.TagText = oldTag;

                if (wnd.ShowDialog() == true && oldTag != wnd.TagText)
                {
                    var response = await client.UnsubscribeAsync(
                        new SubscriprionRequest()
                        {
                            ClientId = Settings.Default.ClientId,
                            Tag = oldTag
                        });

                    if (response.Result)
                    {
                        Settings.Default.Tags.Remove(oldTag);
                        response = await client.SubscribeAsync(new SubscriprionRequest()
                        {
                            ClientId = Settings.Default.ClientId,
                            Tag = wnd.TagText
                        });

                        if (response.Result)
                        {
                            Settings.Default.Tags.Insert(selIndex, wnd.TagText);
                            Settings.Default.Save();
                        }
                        else
                        {
                            ServerErrorUpdateTag();
                        }
                    }
                    else
                    {
                        ServerErrorUpdateTag();
                    }
                }
            }
        }

        private void tagList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            editTagButton.IsEnabled = removeTagButton.IsEnabled =
                tagList.SelectedIndex != -1;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            rssBrowser.NavigateToString("<html><body>" +
                "<div style='padding: 100px; text-align: center; color: #aaa'>Waiting for new RSS feeds *" +
                "<div style='font-size: 12px; text-align: center; margin-top: 10px;'>* You should add tags</div></div>" +
                "</body></html>");
            RebindTags();
            PrepareConnection();
            StartListenFeeds();
            MonitorUpdateRss();
        }
    }
}
