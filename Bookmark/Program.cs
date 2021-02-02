using ServerCommandService;
using ServerCommandWrapper;
using ServerCommandWrapper.Basic;
using ServerCommandWrapper.Ntlm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LoginInfo = ServerCommandWrapper.LoginInfo;


namespace DemoBookmarks
{


    class Program
    {
        private static BasicConnection _basicConnection;
        private static NtlmConnection _ntlmConnection;
        private static readonly Guid IntegrationId = new Guid("5BCB4CD0-2769-4D71-80FA-83CD9F4F6617");
        private const string IntegrationName = "Bookmark Creator";
        private const string Version = "1.0";

        static void Main(string[] args)
        {
            /************************************************************
             * Please change these accordingly to your own environment  *
             ************************************************************/
            String username = "";
            String password = "";
            String domain = "";            

            AuthenticationType authType = AuthenticationType.WindowsDefault;
            String hostAddress = "localhost";
            int port = 80;
            if (authType == AuthenticationType.Basic)
                port = 443; //SSL


            /************************************************************
             * Beginning of program                                     *
             ************************************************************/
            Console.WriteLine("Milestone SDK Bookmarks demo (XProtect Corporate only)");
            Console.WriteLine("Creates 2 new bookmarks and retrieves them using ");
            Console.WriteLine("  1) BookmarkSearchTime ");
            Console.WriteLine("  2) BookmarkSearchFromBookmark");
            Console.WriteLine("  3) BookmarkGet");
            Console.WriteLine("  4) BookmarkDelete");
            Console.WriteLine("");

            
            #region Connect to the Management Server, get configuration, and extract the cameras

            RecorderInfo[] recorderInfo = new RecorderInfo[0];
            LoginInfo loginInfo = null;
            ServerCommandServiceClient scs = null;
            switch (authType)
            {
                case AuthenticationType.Basic:
                    _basicConnection = new BasicConnection(username, password, hostAddress, port);
                    loginInfo = _basicConnection.Login(IntegrationId, Version, IntegrationName);
                    _basicConnection.GetConfiguration(loginInfo.Token);

                    ConfigurationInfo confInfoBasic =
                        _basicConnection.ConfigurationInfo;
                    recorderInfo = confInfoBasic.Recorders;

                    scs = _basicConnection.Server;
                  
                    break;

                case AuthenticationType.Windows:
                case AuthenticationType.WindowsDefault:
                    _ntlmConnection = new NtlmConnection(domain, authType, username, password, hostAddress,
                        port);
                    loginInfo =  _ntlmConnection.Login(IntegrationId, Version, IntegrationName);
                    _ntlmConnection.GetConfiguration(loginInfo.Token);

                    ConfigurationInfo confInfoNtlm = _ntlmConnection.ConfigurationInfo;
                    recorderInfo = confInfoNtlm.Recorders;

                    scs = _ntlmConnection.Server;
                    break;

                default:
                    //Empty
                    break;
            }

            #endregion

            #region Find recording servers attached to the management server 

            //Get recording servers
            int recorders = recorderInfo.Length;
            Console.WriteLine("{0} Corporate Recording Server found", recorders);
            if (recorders == 0)
            {
                Console.WriteLine("");
                Console.WriteLine("Press any key");
                Console.ReadKey();
                return;
            }

            #endregion

        
            DateTime timeNow = DateTime.Now;

            // get cameras for the first recorder
            RecorderInfo recorder = recorderInfo[0];
            Console.WriteLine("");
            Console.WriteLine("Processing recording server {0}", recorder.Name);
            Console.WriteLine("");

            #region Find all cameras defined on the recording server

            // extract info about the recording server
            List<CameraInfo> cameras = recorder.Cameras.ToList();

            #endregion


            
           

            // now-5:10min:                                       BookmarkSearchTime start
            // now-5:00min: (beginTime)                                         |                                                  BookmarkGet
            // now-4:59min: start recording 1                                   |                                       
            // now-4:55min: start bookmark 1                                    |
            // now-4:45min: end bookmark 1                                      |
            //                                                                  |                BookmarkSearchFromBookmark    
            // now-2:00min:                                                     |                            |
            // now-1:59min: start recording 2                                   |                            |
            // now-1:55min: start bookmark 2 (trigger time)                     |                            |
            // now-1:45min: end bookmark 2                                      |                            |
            // now                                                              v                            V

            #region create first bookmark

            Guid cameraGuid = cameras.First().DeviceId;

            Console.WriteLine("Creating the first bookmark");
            MediaDeviceType[] mediaDeviceTypes = new MediaDeviceType[3];
            mediaDeviceTypes[0] = MediaDeviceType.Camera;
            mediaDeviceTypes[1] = MediaDeviceType.Microphone;
            mediaDeviceTypes[2] = MediaDeviceType.Speaker;



            DateTime timeBegin = timeNow.AddMinutes(-5);
            TimeDuration td = new TimeDuration()
            {
                MicroSeconds = (int) TimeSpan.FromMinutes(30).TotalMilliseconds * 1000
            };
            
            StringBuilder bookmarkRef = new StringBuilder();
            StringBuilder bookmarkHeader = new StringBuilder();
            StringBuilder bookmarkDesc = new StringBuilder();
            bookmarkRef.AppendFormat("MyBookmark-{0}", timeBegin.ToLongTimeString());
            bookmarkHeader.AppendFormat("AutoBookmark-{0}", timeBegin.ToLongTimeString());
            bookmarkDesc.AppendFormat("AutoBookmark-{0} set for a duration of {1} seconds",
                timeBegin.ToLongTimeString(), (timeBegin.AddSeconds(10) - timeBegin.AddSeconds(1)).Seconds);

            Bookmark newBookmark = null;
            try
            {
                newBookmark = scs.BookmarkCreate(loginInfo.Token, cameraGuid,
                    timeBegin.AddSeconds(1),
                    timeBegin.AddSeconds(5),
                    timeBegin.AddSeconds(10),
                    bookmarkRef.ToString(),
                    bookmarkHeader.ToString(),
                    bookmarkDesc.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("BookmarkCreate: " + ex.Message);
                Console.WriteLine("Press any Key to exit...");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (newBookmark == null)
            {
                Console.WriteLine("New bookmark wasn't created.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.WriteLine("-> trigger time = {0}", newBookmark.TimeTrigged);
            Console.WriteLine("");


            #endregion

            Console.WriteLine("");
            Console.WriteLine("Waiting 20 sec ....");
            Console.WriteLine("");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(20));

            #region Create a second bookmark

            Console.WriteLine("Creating a second bookmark - 2 minutes after the first bookmark");
            DateTime timeBegin2 = timeBegin.AddMinutes(2);
            bookmarkHeader.Length = 0;
            bookmarkDesc.Length = 0;
            StringBuilder bookmarkRef2 = new StringBuilder();
            bookmarkRef2.AppendFormat("MyBookmark-{0}", timeBegin2.ToLongTimeString());
            bookmarkHeader.AppendFormat("AutoBookmark-{0}", timeBegin2.ToLongTimeString());
            bookmarkDesc.AppendFormat("AutoBookmark-{0} set for a duration of {1} seconds",
                timeBegin2.ToLongTimeString(), (timeBegin2.AddSeconds(10) - timeBegin2.AddSeconds(1)).Seconds);
            Bookmark newBookmark2 = scs.BookmarkCreate(loginInfo.Token, cameraGuid, timeBegin2.AddSeconds(1),
                timeBegin2.AddSeconds(5), timeBegin2.AddSeconds(10)
                , bookmarkRef2.ToString(), bookmarkHeader.ToString(), bookmarkDesc.ToString());

            Console.WriteLine("-> trigger time = {0}", newBookmark2.TimeTrigged);
            Console.WriteLine("");

            #endregion

            #region BookmarkSearchTime

            // Get max 10 of the bookmarks created after the specified time
            Console.WriteLine("");
            Console.WriteLine("Looking for bookmarks using BookmarkSearchTime (finding the 2 newly created)");
            Bookmark[] bookmarkList = scs.BookmarkSearchTime(loginInfo.Token, newBookmark.TimeBegin.AddSeconds(-10), td,
                10, mediaDeviceTypes, new Guid[0], new string[0], "");
            if (bookmarkList.Length > 0)
            {
                Console.WriteLine("-> Found {0} bookmark(s)", bookmarkList.Length);
                int counter = 1;
                foreach (Bookmark bookmark in bookmarkList)
                {
                    Console.WriteLine("{0}:", counter);
                    Console.WriteLine("     Id  ={0} ", bookmark.Id);
                    Console.WriteLine("     Name={0} ", bookmark.Header);
                    Console.WriteLine("     Desc={0} ", bookmark.Description);
                    Console.WriteLine("     user={0} ", bookmark.User);
                    Console.WriteLine("     Device={0} Start={1} Stop={2}  ", bookmark.DeviceId, bookmark.TimeBegin,
                        bookmark.TimeEnd);
                    counter++;
                }
            }
            else
            {
                Console.WriteLine("sorry no bookmarks found");
            }

            Console.WriteLine("");

            #endregion

            #region BookmarkSearchFromBookmark

            // Get the next (max 10) bookmarks after the first
            Console.WriteLine(
                "Looking for bookmarks using BookmarkSearchFromBookmark (finding the last of the 2 newly created)");
            Bookmark[] bookmarkListsFromBookmark = scs.BookmarkSearchFromBookmark(loginInfo.Token, newBookmark.Id, td,
                10, mediaDeviceTypes, new Guid[0], new string[0], "");
            if (bookmarkListsFromBookmark.Length > 0)
            {
                Console.WriteLine("-> Found {0} bookmark(s)", bookmarkListsFromBookmark.Length);
                int counter = 1;
                foreach (Bookmark bookmark in bookmarkListsFromBookmark)
                {
                    Console.WriteLine("{0}:", counter);
                    Console.WriteLine("     Id  ={0} ", bookmark.Id);
                    Console.WriteLine("     Name={0} ", bookmark.Header);
                    Console.WriteLine("     Desc={0} ", bookmark.Description);
                    Console.WriteLine("     user={0} ", bookmark.User);
                    Console.WriteLine("     Device={0} Start={1} Stop={2}  ", bookmark.DeviceId, bookmark.TimeBegin,
                        bookmark.TimeEnd);
                    counter++;
                }
            }
            else
            {
                Console.WriteLine("sorry no bookmarks found");
            }

            Console.WriteLine("");

            #endregion

            #region BookmarkGet

            // Get first created bookmark
            Console.WriteLine(
                "Looking for the first bookmarks using BookmarkGet  (finding the first of the 2 newly created)");
            Bookmark newBookmarkFetched = scs.BookmarkGet(loginInfo.Token, newBookmark.Id);
            if (newBookmarkFetched != null)
            {
                Console.WriteLine("-> A bookmarks is found");
                Console.WriteLine("     Id  ={0} ", newBookmarkFetched.Id);
                Console.WriteLine("     Name={0} ", newBookmarkFetched.Header);
                Console.WriteLine("     Desc={0} ", newBookmarkFetched.Description);
                Console.WriteLine("     user={0} ", newBookmarkFetched.User);
                Console.WriteLine("     Device={0} Start={1} Stop={2}  ", newBookmarkFetched.DeviceId,
                    newBookmarkFetched.TimeBegin, newBookmarkFetched.TimeEnd);
            }
            else
            {
                Console.WriteLine("Sorry no bookmarks found");
            }

            Console.WriteLine("");

            #endregion

            #region Deleting bookmarks

            Console.WriteLine("Deleting 2 newly created bookmarks");
            scs.BookmarkDelete(loginInfo.Token, newBookmark.Id);
            Console.WriteLine("   -> first deleted");
            scs.BookmarkDelete(loginInfo.Token, newBookmark2.Id);
            Console.WriteLine("   -> second deleted");

            #endregion



            Console.WriteLine("");
            Console.WriteLine("Press any key");
            Console.ReadKey();
        }
    }
}
