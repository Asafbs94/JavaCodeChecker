using JavaCodeChecker.Common;
using JavaCodeChecker.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;

namespace JavaCodeChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //private variables to store relevant information
        private int recordCount = 0;

        private string path;
        private int currentYear = 0;
        private string courseName;
        private string hwName;
        private List<GradingRules> GradingRules = new List<GradingRules>();

        private ObservableCollection<StudentGradeModel> StudentGradesInAllCourseHomeWorks = new ObservableCollection<StudentGradeModel>();
        private ObservableCollection<StudentGradeModel> StudentGrades = new ObservableCollection<StudentGradeModel>();
        private List<CourseAverageGrade> CourseAverageGrades = new List<CourseAverageGrade>();

        public MainWindow()
        {
            InitializeComponent();

            //read configured path from app.config and set the path in textbox
            path = ConfigUtil.GetSetting("path");

            tbPath.Text = path;

            //set visibility of busy indicatior to hidden
            busyIndicator.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// event listner to load files button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            // if there is no path selected, dont proceed
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Please select a valid path first");
                return;
            }

            // if there is a path set busy indicator visibility to visible
            busyIndicator.Visibility = Visibility.Visible;

            // start the process of calculating grades for course homeworks
            new Task(StartProcess).Start();
        }

        /// <summary>
        /// StartProcess
        /// </summary>
        private void StartProcess()
        {
            _ = Task.Run(() =>
            {
                DateTime dateTime;

                //get all the files and folders in configured path
                var filesAndFolders = GetAllDirectoriesInThePath(path);

                foreach (var folder in filesAndFolders.OrEmptyIfNull())
                {
                    // loop through all files and folders and check if there is a folder
                    if ((folder.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        // get year name from directory name
                        var tokens = folder.Name.Split('-');
                        var year = tokens[tokens.Length - 1];

                        // check if valid year
                        if (DateTime.TryParse(string.Format("1/1/{0}", year), out dateTime))
                        {
                            currentYear = dateTime.Year;

                            // check if current directory is empth and dont proceed
                            if (IsDirectoryEmpty(path))
                            {
                                MessageBox.Show("Folder is empty, please add required file");
                                return;
                            }
                            else
                            {
                                // get all the files and folders inside year folder and these all are courses
                                var courses = GetAllDirectoriesInThePath(folder.FullName);

                                // loop through all files and folders and check if there is a folder, each folder is
                                // a course
                                foreach (var course in courses.OrEmptyIfNull())
                                {
                                    if ((course.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                                    {
                                        // get the course name from directory name
                                        var courseTokens = course.Name.Split('_');

                                        courseName = courseTokens[0];

                                        // process current course folder in the loop
                                        ProceedWithCurrentCourse(course);
                                    }
                                }
                            }
                        }
                        else
                            MessageBox.Show("Invalid path, root folder name must be an year");
                    }
                }

                // after process is completed, assign the source to data grid and set the visibility of
                // busy indicator to hidden with a dispathcer to avoid deadlock
                this.Dispatcher.Invoke(() =>
                {
                    busyIndicator.Visibility = Visibility.Hidden;
                    dgCourseGrades.ItemsSource = CourseAverageGrades;
                });

                // if there are any records, save those in the database by calling the Rest API method
                if (StudentGrades.Count > 0)
                {
                    try
                    {
                        var response = WebApi.PostCall(ApiUrls.StudentGrade, CourseAverageGrades);

                        if (response.Result.StatusCode == System.Net.HttpStatusCode.Created)
                        {
                            MessageBox.Show("Saved in database!");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            });
        }

        /// <summary>
        /// SaveAverageCourseGrade
        /// </summary>
        /// <param name="courseEntries"></param>
        private void SaveAverageCourseGrade(List<FileSystemInfo> courseEntries)
        {
            // group the records on student name to get average grade
            var groupedItems = StudentGradesInAllCourseHomeWorks.GroupBy(x => x.StudentName);

            // loop through the grouped records to populate our list to save into database and to a csv file
            foreach (var courseGrade in groupedItems)
            {
                CourseAverageGrades.Add(new CourseAverageGrade
                {
                    StudentName = courseGrade.Key,
                    CourseName = courseGrade.FirstOrDefault().CourseName,
                    StudentId = courseGrade.FirstOrDefault().StudentId,
                    AverageGrade = courseGrade.Sum(x => x.Grade),
                    Year = currentYear.ToString()
                });
            }

            // save the average grade records of each student into course_grades.csv file
            if (CourseAverageGrades.Count > 0)
            {
                var course_grades = courseEntries.FirstOrDefault(x => x.Name == "course_grades.csv");

                if (course_grades.Exists)
                {
                    SaveToCsv<CourseAverageGrade>(CourseAverageGrades.ToList(), course_grades.FullName);
                }
            }
        }

        /// <summary>
        /// ProceedWithCurrentCourse
        /// </summary>
        /// <param name="course"></param>
        private void ProceedWithCurrentCourse(FileSystemInfo course)
        {
            // get all the files and folders inside course folder
            var courseEntries = GetAllDirectoriesInThePath(course.FullName);

            // loop through all files and folders inside course folder and check if there is a folder, each folder is
            // a homework
            foreach (var directoryItem in courseEntries.OrEmptyIfNull())
            {
                if (directoryItem.Name == "course_info.json")
                {
                    //TODO: nothing needed atm
                }
                else if (directoryItem.Name == "course_grades.csv")
                {
                    //TODO: nothing needed atm
                }
                else if ((directoryItem.Attributes & FileAttributes.Directory) == FileAttributes.Directory
                                && directoryItem.Name.ToLower().Contains("hw")) // if its folder and name contains homework alias
                {
                    // get the homework name, which is direcotyr name
                    hwName = directoryItem.Name;

                    // get all the files and folders inside homework folder
                    var hwDirectoryItems = GetAllDirectoriesInThePath(directoryItem.FullName);

                    //check if current homeowrk direcoty contains rules json file
                    var rulesFile = hwDirectoryItems
                        .Where(x => x.Name.ToLower() == "rules.json")?.FirstOrDefault();

                    if (rulesFile != null)
                    {
                        // if file exists, read all the json rules from json file to deserialize it
                        var json = File.ReadAllText(rulesFile.FullName);
                        // parse the json
                        IDictionary<string, JToken> Jsondata = JObject.Parse(json);
                        GradingRules.Clear();

                        // create our list of rules from json data
                        foreach (var item in Jsondata.DefaultIfEmpty())
                        {
                            GradingRules.Add(new GradingRules
                            {
                                RuleName = item.Key,
                                RuleExpression = item.Value.ToString()
                            });
                        }

                        // calculate grades for all students inside homework folder
                        CalculateGrades(hwDirectoryItems);
                    }
                    else
                    {
                        // if rules json file is not found
                        MessageBox.Show($"Rules file is not found for {hwName}");
                    }
                }
            }

            // call the method to save the average grades of each student into csv file
            SaveAverageCourseGrade(courseEntries);

            StudentGradesInAllCourseHomeWorks.Clear();
        }

        /// <summary>
        /// CalculateGrades
        /// </summary>
        /// <param name="hwDirectoryItems"></param>
        private void CalculateGrades(List<FileSystemInfo> hwDirectoryItems)
        {
            // calculate grades for all students inside homework directory
            StudentGrades.Clear();

            // loop through all files and folders inside homework folder and check if there is a folder, each folder is
            // a  submitted by a student
            foreach (var hwDirectoryItem in hwDirectoryItems.DefaultIfEmpty())
            {
                if ((hwDirectoryItem.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    CheckRulesAndCalculateGradeForStudentHw(hwDirectoryItem);
                }
            }

            // if there is any record found for this homework, save it in csv file
            if (StudentGrades.Count > 0 && hwDirectoryItems.Count > 0)
            {
                var course_grades = hwDirectoryItems.FirstOrDefault(x => x.Name == "grades.csv");

                if (course_grades.Exists)
                {
                    SaveToCsv<StudentGradeModel>(StudentGrades.ToList(), course_grades.FullName);
                }
            }

            // save current student data to a main list where we are storing data for all homeworks and courses
            // so check if we are not adding duplicate record
            if (StudentGrades.Count > 0)
            {
                foreach (var item in StudentGrades)
                {
                    if (!StudentGradesInAllCourseHomeWorks.Any(x => x.StudentName == item.StudentName &&
                            x.CourseName == item.CourseName && x.HomeworkName == item.HomeworkName))
                    {
                        StudentGradesInAllCourseHomeWorks.Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// CheckRulesAndCalculateGradeForStudentHw
        /// </summary>
        /// <param name="hwDirectoryItem"></param>
        private void CheckRulesAndCalculateGradeForStudentHw(FileSystemInfo hwDirectoryItem)
        {
            // get the student name and id from folder name which is inside homework folder
            var hwFolderNameTokes = hwDirectoryItem.Name.Split('-');

            var studentName = hwFolderNameTokes[0].Trim();
            var studentId = hwFolderNameTokes[1].Trim();

            // get all the files and folders inside student's homework folder, there should be java file
            var hwFiles = GetAllDirectoriesInThePath(hwDirectoryItem.FullName);

            // if there is no file/folder, assign 0 grade to student and add record
            if (!hwFiles.Any())
            {
                StudentGrades.Add(new StudentGradeModel
                {
                    CourseName = courseName,
                    Feedback = $"Homework folder {hwDirectoryItem.Name} is empty",
                    Grade = 0,
                    HomeworkName = hwName,
                    StudentName = studentName,
                    StudentId = studentId
                });
            }
            else
            {
                // apply all rules saved in our GradingRules list which we read from json file
                double grade = 0;
                var javaFiles = hwFiles.Where(x => x.Extension.ToLower() == ".java")?.ToList();

                foreach (var rule in GradingRules)
                {
                    var ruleName = rule.RuleName.ToLower();

                    // if current rule is to check if file exist
                    if (ruleName.Contains("file exists"))
                    {
                        if (hwFiles.Any(x => x.Extension.ToLower() == ".java"))
                        {
                            grade += Convert.ToDouble(rule.RuleExpression);
                        }
                    }// if we are going to check a regex if its in the java file submitted by the student
                    else if (ruleName == "regex")
                    {
                        bool found = false;

                        foreach (var javafile in javaFiles.DefaultIfEmpty())
                        {
                            var fileContent = File.ReadAllText(javafile.FullName);

                            //var regexExcpression = Regex.Replace(rule.RuleExpression, "[^\\w\\d]", "");
                            var regexExcpression = rule.RuleExpression.Trim('/').Trim('$').Trim('^');

                            // check if regex is matching to the content of java file
                            var match = Regex.Match(fileContent, regexExcpression, RegexOptions.IgnoreCase);

                            // if regex matches, then set the flag value and terminate the loop
                            if (match.Success)
                            {
                                found = true;
                                break;
                            }
                        }

                        // if regex was matched, add assigned grade to student's grade from json file
                        if (found)
                        {
                            var regexGradeRule = GradingRules
                                    .Where(x => x.RuleName.ToLower() == "regex exist")?.FirstOrDefault();
                            if (regexGradeRule != null)
                            {
                                grade += Convert.ToDouble(regexGradeRule.RuleExpression);
                            }
                        }
                    }
                    // if current rule is to check if code is compiling, this feature is not working 100% but it works
                    else if (ruleName.Contains("code compile"))
                    {
                        try
                        {
                            // get the java file from student's homework and copy it to working direcoty of our
                            // project to compile
                            var javaFile = javaFiles.FirstOrDefault();
                            File.Copy(javaFile.FullName, Directory.GetCurrentDirectory()+"\\"+javaFile.Name, true);

                            // start a process to initiate command prompt in hidden mode
                            Process cmd = new Process();
                            cmd.StartInfo.FileName = "cmd.exe";
                            cmd.StartInfo.RedirectStandardInput = true;
                            cmd.StartInfo.RedirectStandardOutput = true;
                            cmd.StartInfo.CreateNoWindow = true;
                            cmd.StartInfo.UseShellExecute = false;

                            cmd.Start();

                            // push JAVAC command into the command line with file name to compile java code
                            cmd.StandardInput.WriteLine($@"javac " +javaFile.Name);

                            // flush the memory to prevent memory leakage
                            cmd.StandardInput.Flush();
                            cmd.StandardInput.Close();

                            // read the output from command line and check if there is any error,
                            // if there is no error, then add the score into grade
                            var output = cmd.StandardOutput.ReadToEnd();

                            if (output != null && !output.Contains("error"))
                            {
                                grade += Convert.ToDouble(rule.RuleExpression);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }

                // after evaluating all rules, add the record for current student to a list
                StudentGrades.Add(new StudentGradeModel
                {
                    CourseName = courseName,
                    Feedback = $"Homework {hwDirectoryItem.Name} is graded",
                    Grade = grade,
                    HomeworkName = hwName,
                    StudentName = studentName,
                    StudentId = studentId
                });
            }
        }

        /// <summary>
        /// GetAllDirectoriesInThePath
        /// </summary>
        /// <param name="_path"></param>
        /// <returns></returns>
        private List<FileSystemInfo> GetAllDirectoriesInThePath(string _path)
        {
            // read all files and folders for a given path and return
            var directoriesAndFiles = new DirectoryInfo(_path)
                .EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly);

            return directoriesAndFiles.ToList();
        }

        /// <summary>
        /// btnSelect_Click when we click select path button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            // Create FolderBrowserDialog
            FolderBrowserDialog dlg = new FolderBrowserDialog();

            // Display FolderBrowserDialog by calling ShowDialog method
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == System.Windows.Forms.DialogResult.OK
                && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                path = dlg.SelectedPath;

                tbPath.Text = path;
            }
        }

        public bool IsDirectoryEmpty(string path)
        {
            IEnumerable<string> items = Directory.EnumerateFileSystemEntries(path);

            using (IEnumerator<string> en = items.GetEnumerator())
            {
                return !en.MoveNext();
            }
        }

        /// <summary>
        /// a generic method to save list of objects as csv file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reportData"></param>
        /// <param name="path"></param>
        private void SaveToCsv<T>(List<T> reportData, string path)
        {
            var lines = new List<string>();
            IEnumerable<PropertyDescriptor> props = TypeDescriptor.GetProperties(typeof(T)).OfType<PropertyDescriptor>();
            var header = string.Join(",", props.ToList().Select(x => x.Name));
            lines.Add(header);
            var valueLines = reportData.Select(row => string.Join(",", header.Split(',').Select(a => row.GetType().GetProperty(a).GetValue(row, null))));
            lines.AddRange(valueLines);
            File.WriteAllLines(path, lines.ToArray());
        }

        /// <summary>
        /// OnClosing
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(CancelEventArgs e)
        {
            // set the selected path to config file which exiting
            ConfigUtil.SetSetting("path", tbPath.Text);
            base.OnClosing(e);
        }
    }
}