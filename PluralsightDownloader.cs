using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using System.Collections.Generic;

public class Course
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
}

public class Clip
{
    public string Author { get; set; }
    public string Name { get; set; }
    public int Num { get; set; }
    public string Course { get; set; }

    public string Title { get; set; }
    public string Url { get; set; }

    public string ReplacementUrl
    {
        get
        {
            return String.Format(
                "window.open('{0}{1}.mp4')",
                Name, Num
            );
        }
    }

    public string Source
    {
        get
        {
            return String.Format(
                "http://pluralsight.com/training/Player?author={0}&name={1}&mode=live&clip={2}&course={3}",
                Author, Name, Num, Course
            );
        }
    }

    public string FileName
    {
        get
        {
            return Name + Num + ".mp4";
        }
    }
}

public class Logger
{
    public static string LogFile { get; set; }
    public static bool LogToFileEnabled { get; set; }
    public static bool LogToConsoleEnabled { get; set; }

    public static void Log(string msg)
    {
        if (LogToConsoleEnabled)
        {
            Console.WriteLine(msg);
        }

        if (LogToFileEnabled)
        {
            File.AppendAllText(LogFile, msg + Environment.NewLine);
        }
    }
}

public class PluralsightDownloader
{
    private const string BaseUrl = "http://www.pluralsight.com";
    private const string CourseListUrl = "http://www.pluralsight.com/training/Courses/";
    private const string ClipRequestUrl = "http://pluralsight.com/training/Player/ViewClip";
    private const string AuthCookie = @"PSM=1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
    private const string CoursesHtmlFile = "courses.html";
    private const string CoursesCsvFile = "courses.csv";
    private const string ClipsHtmlFile = "index.html";
    private const string ClipsCsvFile = "index.csv";
    private const string FirstCourseId = "dotnet-reflector-by-example";
    private const string LastCourseId = "xml-fund";
    private const string Delimiter = "|";

    public static void Main(string[] args)
    {
        var client = new WebClient();
        client.Headers[HttpRequestHeader.Cookie] = AuthCookie;
        Logger.LogFile = "log" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
        Logger.LogToConsoleEnabled = true;
        Logger.LogToFileEnabled = true;

        var courses = GetCourseList(client);
        var courseNum = 1;
        foreach (Course course in courses)
        {
            Logger.Log(String.Format(Environment.NewLine + "Course {0} ({1}/{2})", course.Id, courseNum, courses.Count));

            var clips = GetClipList(client, course);
            var clipNum = 1;
            foreach (Clip clip in clips)
            {
                Logger.Log(String.Format("Downloading {0} ({1}/{2})...", clip.FileName, clipNum, clips.Count));
                DownloadClip(client, clip);
                clipNum++;
            }

            Logger.Log(String.Format("Course {0} ({1}/{2}) finished.", course.Id, courseNum, courses.Count));
            //Logger.Log("Press enter to continue or Ctrl+C to cancel...");
            //Console.ReadLine();
            courseNum++;
        }
    }

    public static ICollection<Course> GetCourseList(WebClient client)
    {
        ICollection<Course> courses;

        if (!File.Exists(CoursesCsvFile))
        {
            var coursesHtml = "";
            var coursesCsv = "";

            Logger.Log("Downloading course list...");
            coursesHtml = client.DownloadString(CourseListUrl);

            Logger.Log("Parsing course list...");
            courses = GetCoursesFromHtml(coursesHtml);
            Logger.Log(courses.Count + " courses found.");

            Logger.Log("Cleaning up course list...");
            courses = CleanCourses(courses);
            Logger.Log(courses.Count + " courses remaining.");

            Logger.Log("Creating " + CoursesHtmlFile + " and " + CoursesCsvFile + "...");
            foreach (Course course in courses)
            {
                coursesHtml = coursesHtml.Replace(course.Url, course.Id + '/');
                coursesCsv += String.Join(Delimiter, course.Id, course.Name, course.Url) + Environment.NewLine;
            }
            File.WriteAllText(CoursesHtmlFile, coursesHtml);
            File.WriteAllText(CoursesCsvFile, coursesCsv);
        }
        else
        {
            Logger.Log("Loading course list from " + CoursesCsvFile + "...");
            courses = GetCoursesFromCsv(CoursesCsvFile);
            Logger.Log(courses.Count + " courses found.");
        }

        return courses;
    }

    public static ICollection<Clip> GetClipList(WebClient client, Course course)
    {
        ICollection<Clip> clips;

        if (!Directory.Exists(course.Id))
        {
            Logger.Log("Creating " + course.Id + " directory...");
            Directory.CreateDirectory(course.Id);
        }

        if (!File.Exists(course.Id + "/" + ClipsCsvFile))
        {
            var clipsHtml = "";
            var clipsCsv = "";

            Logger.Log("Downloading clips list...");
            clipsHtml = client.DownloadString(BaseUrl + course.Url);

            Logger.Log("Parsing clips...");
            clips = GetClipsFromHtml(clipsHtml);
            Logger.Log(clips.Count + " clips found.");

            Logger.Log("Cleaning up clips...");
            clips = CleanClips(clips);
            Logger.Log(clips.Count + " clips remaining.");

            Logger.Log("Creating " + ClipsHtmlFile + " and " + ClipsCsvFile + "...");
            foreach (Clip clip in clips)
            {
                clipsHtml = clipsHtml.Replace(clip.Url, clip.ReplacementUrl);
                clipsCsv += String.Join(Delimiter, clip.Author, clip.Name, clip.Num, clip.Course, clip.Title, clip.Url, clip.ReplacementUrl, clip.FileName, clip.Source) + Environment.NewLine;
            }
            File.WriteAllText(course.Id + "/" + ClipsHtmlFile, clipsHtml);
            File.WriteAllText(course.Id + "/" + ClipsCsvFile, clipsCsv);
        }
        else
        {
            Logger.Log("Loading clip list from " + course.Id + "/" + ClipsCsvFile + "...");
            clips = GetClipsFromCsv(course.Id + "/" + ClipsCsvFile);
            Logger.Log(clips.Count + " clips found.");
        }

        return clips;
    }

    public static ICollection<Course> GetCoursesFromHtml(string content)
    {
        var matches = Regex.Matches(content, @"(/training/Courses/TableOfContents/([^""']*))""[^>]*>([^<]*)");

        var courses = new List<Course>();
        foreach (Match match in matches)
        {
            courses.Add(new Course()
            {
                Url = match.Groups[1].Value,
                Id = match.Groups[2].Value,
                Name = match.Groups[3].Value
            });
        }

        return courses;
    }

    public static ICollection<Course> GetCoursesFromCsv(string file)
    {
        var query = from line in File.ReadAllLines(file)
                    let data = line.Split(Delimiter.ToCharArray())
                    select new Course
                    {
                        Id = data[0],
                        Name = data[1],
                        Url = data[2]
                    };

        var result = query.ToList<Course>();

        return result;
    }

    public static ICollection<Course> CleanCourses(ICollection<Course> courses)
    {
        List<Course> coursesList = (List<Course>)courses;
        var startIndex = coursesList.FindIndex(c => c.Id == FirstCourseId);
        var endIndex = coursesList.FindIndex(c => c.Id == LastCourseId);
        var result = (ICollection<Course>)coursesList.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
        return result;
    }

    public static ICollection<Clip> GetClipsFromHtml(string content)
    {
        var matches = Regex.Matches(content, @"(LaunchSelectedPlayer\('author=([^&]*)&amp;name=([^&]*)&amp;mode=live&amp;clip=([^&]*)&amp;course=([^']*)'\))[^>]*>([^<]*)");

        var clips = new List<Clip>();
        foreach (Match match in matches)
        {
            clips.Add(new Clip()
            {
                Url = match.Groups[1].Value,
                Author = match.Groups[2].Value,
                Name = match.Groups[3].Value,
                Num = Convert.ToInt32(match.Groups[4].Value),
                Course = match.Groups[5].Value,
                Title = match.Groups[6].Value
            });
        }

        return clips;
    }

    public static ICollection<Clip> GetClipsFromCsv(string file)
    {
        var query = from line in File.ReadAllLines(file)
                    let data = line.Split(Delimiter.ToCharArray())
                    select new Clip
                    {
                        Author = data[0],
                        Name = data[1],
                        Num = Convert.ToInt32(data[2]),
                        Course = data[3],
                        Title = data[4],
                        Url = data[5]
                    };

        var result = query.ToList<Clip>();

        return result;
    }

    public static ICollection<Clip> CleanClips(ICollection<Clip> clips)
    {
        return clips.Where(clip => !String.IsNullOrWhiteSpace(clip.Title)).ToList();
    }

    public static void DownloadClip(WebClient client, Clip clip)
    {
        // Request Header
        // Content-Type: application/json;charset=utf-8
        // Cookie: PSM=ABCDE.....
        //
        // Request Body
        // {a:"scott-allen", m:"mvc-ajax", course:"aspdotnet-mvc-advanced-topics", cn:1, mt:"mp4", q:"1024x768", cap:false, lc:"en"}

        client.Headers.Add("Content-Type", "application/json");
        var request = String.Format(
            @"{{a:""{0}"", m:""{1}"", course:""{2}"", cn:{3}, mt:""mp4"", q:""1024x768"", cap:false, lc:""en""}}",
            clip.Author, clip.Name, clip.Course, clip.Num);

        Logger.LogToConsoleEnabled = false;
        Logger.Log("Request: " + request);
        var response = client.UploadString(ClipRequestUrl, request);
        Logger.Log("Response: " + response);
        Logger.LogToConsoleEnabled = true;

        client.DownloadFile(response, clip.Course + "/" + clip.FileName);
    }
}
