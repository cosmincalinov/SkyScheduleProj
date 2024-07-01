using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Data.Entity;
using System.Xml.Linq;

namespace SkyScheduleProj
{
    public partial class MainWindow : Window
    {
        private string currentUserEmail;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnSignup_Click(object sender, RoutedEventArgs e)
        {
            signUpTab.Focus();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string inEmail = loginEmailBox.Text;
            string inPass = loginPswdBox.Password;
            using (SkyScheduleEntities ent = new SkyScheduleEntities())
            {
                var user = ent.Users.SingleOrDefault(o => o.email == inEmail);

                if (user == null)
                {
                    MessageBox.Show("Email isn't registered! Consider signing up!");
                    return;
                }

                if (user.password != inPass)
                {
                    MessageBox.Show("Incorrect password!");
                    return;
                }

                currentUserEmail = inEmail;
                LoadUserSchedules();
                schedulesTab.Focus();
            }
        }

        private void btnSignup2_Click(object sender, RoutedEventArgs e)
        {
            string email = signUpEmailBox.Text;
            string username = usernameBox.Text;
            string password = signUpPswdBox.Password;
            string confirmPassword = confirmPswdBox.Password;

            if (password != confirmPassword)
            {
                MessageBox.Show("The passwords don't match!");
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("No email provided!");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("No password provided!");
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("No username provided!");
                return;
            }

            using (SkyScheduleEntities ent = new SkyScheduleEntities())
            {
                if (ent.Users.Any(u => u.email == email))
                {
                    MessageBox.Show("This email is already registered!");
                    return;
                }

                var user = new User
                {
                    email = email,
                    username = username,
                    password = password
                };

                ent.Users.Add(user);
                ent.SaveChanges();

                MessageBox.Show("Sign up successful. You can now log in.");
            }
        }

        private void btnNewSchedule_Click(object sender, RoutedEventArgs e)
        {
            titleBox.Clear();
            foreach (var child in templateOrar.Children)
            {
                if (child is TextBox textBox)
                {
                    textBox.Clear();
                }
            }
            editorTab.Focus();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            schedulesTab.Focus();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            using (SkyScheduleEntities ent = new SkyScheduleEntities())
            {
                var user = ent.Users.Include(u => u.Schedules).SingleOrDefault(u => u.email == currentUserEmail);
                if (user != null)
                {
                    var schedules = user.Schedules.ToList();
                    ExportSchedulesToPdf(schedules);
                }
                else
                {
                    MessageBox.Show("Error: User not found.");
                }
            }
        }

        private void ExportSchedulesToPdf(List<Schedule> schedules)
        {
            PdfDocument document = new PdfDocument();
            document.Info.Title = "My Schedules";

            foreach (var schedule in schedules)
            {
                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);
                RenderGridToPdf(gfx, templateOrar);
            }

            string filename = "MySchedules.pdf";
            document.Save(filename);
            Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
        }

        private void RenderGridToPdf(XGraphics gfx, Grid grid)
        {
            double cellWidth = gfx.PageSize.Width / grid.ColumnDefinitions.Count;
            double cellHeight = gfx.PageSize.Height / grid.RowDefinitions.Count;

            foreach (var child in grid.Children)
            {
                if (child is TextBox textBox)
                {
                    int row = Grid.GetRow(textBox);
                    int column = Grid.GetColumn(textBox);
                    double x = column * cellWidth;
                    double y = row * cellHeight;
                    gfx.DrawRectangle(XPens.Black, new XRect(x, y, cellWidth, cellHeight));
                    gfx.DrawString(textBox.Text, new XFont("Verdana", 12), XBrushes.Black, new XRect(x, y, cellWidth, cellHeight), XStringFormats.Center);
                }
                else if (child is TextBlock textBlock)
                {
                    int row = Grid.GetRow(textBlock);
                    int column = Grid.GetColumn(textBlock);
                    double x = column * cellWidth;
                    double y = row * cellHeight;
                    gfx.DrawRectangle(XPens.Black, new XRect(x, y, cellWidth, cellHeight));
                    gfx.DrawString(textBlock.Text, new XFont("Verdana", 12), XBrushes.Black, new XRect(x, y, cellWidth, cellHeight), XStringFormats.Center);
                }
            }
        }


        private void btnSaveSchedule_Click(object sender, RoutedEventArgs e)
        {
            string title = titleBox.Text;

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter a title for the schedule.");
                return;
            }

            using (SkyScheduleEntities ent = new SkyScheduleEntities())
            {
                var user = ent.Users.SingleOrDefault(u => u.email == currentUserEmail);
                if (user != null)
                {
                    string path = SaveScheduleLocally(title);
                    var schedule = new Schedule
                    {
                        path = path,
                        user_id = user.id
                    };

                    ent.Schedules.Add(schedule);
                    ent.SaveChanges();

                    LoadUserSchedules();
                    MessageBox.Show("Schedule saved successfully.");
                }
                else
                {
                    MessageBox.Show("Error: User not found.");
                }
            }
        }

        private string SaveScheduleLocally(string title)
        {
            string filePath = $"{title}.xml";
            XDocument doc = new XDocument(
                new XElement("Schedule",
                    new XElement("Title", title),
                    new XElement("Entries",
                        from TextBox textBox in templateOrar.Children.OfType<TextBox>()
                        select new XElement("Entry",
                            new XElement("Row", Grid.GetRow(textBox)),
                            new XElement("Column", Grid.GetColumn(textBox)),
                            new XElement("Text", textBox.Text)
                        )
                    )
                )
            );
            doc.Save(filePath);
            return filePath;
        }

        private void LoadScheduleLocally(string path)
        {
            if (File.Exists(path))
            {
                XDocument doc = XDocument.Load(path);
                titleBox.Text = doc.Root.Element("Title").Value;
                foreach (var entry in doc.Root.Element("Entries").Elements("Entry"))
                {
                    int row = int.Parse(entry.Element("Row").Value);
                    int column = int.Parse(entry.Element("Column").Value);
                    string text = entry.Element("Text").Value;

                    foreach (var child in templateOrar.Children)
                    {
                        if (child is TextBox textBox && Grid.GetRow(textBox) == row && Grid.GetColumn(textBox) == column)
                        {
                            textBox.Text = text;
                            break;
                        }
                    }
                }
            }
        }

        private void lstSchedules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSchedules.SelectedItem is Schedule selectedSchedule)
            {
                LoadScheduleLocally(selectedSchedule.path);
                editorTab.Focus();
            }
        }

        private void LoadUserSchedules()
        {
            using (SkyScheduleEntities ent = new SkyScheduleEntities())
            {
                var user = ent.Users.Include(u => u.Schedules).SingleOrDefault(u => u.email == currentUserEmail);
                if (user != null)
                {
                    lstSchedules.ItemsSource = user.Schedules.ToList();
                    lstSchedules.DisplayMemberPath = "path";
                }
            }
        }
    }
}
