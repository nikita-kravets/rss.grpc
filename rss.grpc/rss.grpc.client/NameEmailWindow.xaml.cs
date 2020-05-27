using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace rss.grpc.client
{
    /// <summary>
    /// Логика взаимодействия для NameEmailWindow.xaml
    /// </summary>
    public partial class NameEmailWindow : Window
    {
        public string ClientName { get; set; }
        public string Email { get; set; }

        public NameEmailWindow()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            //all optional, no validaition needed
            ClientName = clientNameText.Text;
            Email = emailText.Text;
            DialogResult = true;
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
