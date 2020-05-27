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
    /// Логика взаимодействия для TagEditWindow.xaml
    /// </summary>
    public partial class TagEditWindow : Window
    {
        public string TagText { get; set; }
        public TagEditWindow()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(tagText.Text.Trim()))
            {
                DialogResult = true;
                TagText = tagText.Text;
                Close();
            }
            else
            {
                MessageBox.Show("Tag should not be empty!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TagText))
            {
                tagText.Text = TagText;
            }
        }
    }
}
