using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SprytCore
{
    class SprytCore
    {


        private TcpListener Listener;  

        static void Main(string[] args)
        {
            new SprytCore();
        }


        //sprytcore 스레드 시작
        public SprytCore()
        {
            try
            {
                string ip = getExternalIp();
                
                //string ip = ("172.30.1.2");
                Listener = new TcpListener(System.Net.IPAddress.Parse(ip), 1539);
                Listener.Start(); 
                Console.WriteLine("서버 실행 중 : " + ip);

                Thread th = new Thread(new ThreadStart(StartListen));
                th.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("리스닝 중 오류 발생 :" + e.ToString());
                Console.ReadKey();
            }
        }

        
        //ip주소 얻어오기
        private string getExternalIp()
        {
            try
            {
                string externalIP;
                externalIP = (new System.Net.WebClient()).DownloadString("http://checkip.dyndns.org/");
                externalIP = (new System.Text.RegularExpressions.Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"))
                             .Matches(externalIP)[0].ToString();
                return externalIP;
            }
            catch { return null; }
        }
        //서버 디렉토리 읽어오기
        public string rootdir()
        {
            //파일 저장 / 읽을 주소
            string serverpgroot = Directory.GetCurrentDirectory() + @"\locate.svr";
            //생존여부확인
            if (File.Exists(serverpgroot) == true)
            {
                System.IO.StreamReader serverpath = 
                    new System.IO.StreamReader(serverpgroot);
                Console.WriteLine("현재 서버 경로 파일 위치:" + serverpgroot);
                string sevrootdir = serverpath.ReadLine();
                Console.WriteLine("설정된 서버 경로:" + sevrootdir);
                return sevrootdir;
            }
                //사망시 부활
            else
            {
                Console.WriteLine("서버 경로를 지정해 주세요. \n서버 경로 저장 파일은 locate.svr 파일로 서버 프로그램 위치에 저장됩니다.");
                Console.WriteLine(@"ex)c:\www");
                Console.WriteLine("참고) 빈칸으로 입력시 서버 프로그램이 존재하는곳을 루트디렉터리로 만듭니다.");
                string sevrootdir = Console.ReadLine();
                //귀찮은 그대를 위하여
                if (sevrootdir.Length == 0)
                {
                    sevrootdir = Directory.GetCurrentDirectory() + @"/";
                    using (FileStream fs = File.Create(serverpgroot))
                    {
                        Byte[] txt = new UTF8Encoding(true).GetBytes(sevrootdir);
                        fs.Write(txt, 0, txt.Length);
                    }
                    Console.WriteLine("설정된 서버 경로:" + sevrootdir);
                    return sevrootdir;

                }
                    //주소를 설정할줄 정말몰랐어.
                else
                {
                    sevrootdir = sevrootdir + @"/";
                    using (FileStream fs = File.Create(serverpgroot))
                    {
                        Byte[] txt = new UTF8Encoding(true).GetBytes(sevrootdir);
                        fs.Write(txt, 0, txt.Length);
                    }
                    Console.WriteLine("설정된 서버 경로:" + sevrootdir);
                    return sevrootdir;
                }
            }
        }
        //서버 디렉토리 유효성확인
        public string GetLocalPath(string sDirName)
        {
            string WebServerRoot = rootdir();
            String sRealDir = "";

            sDirName.Trim();
            WebServerRoot = WebServerRoot.ToLower();
            sDirName = sDirName.ToLower();

            try
            {
                sRealDir = WebServerRoot + sDirName.Replace("/", "\\");
            }
            catch (Exception e)
            {
                Console.WriteLine("오류 : " + e.ToString());
            }
            
            if (Directory.Exists(sRealDir))
                return sRealDir;
            else
                return "";
        }
        //mime 값 지정
        string GetMimeType(string FilePath)
        {

            FileInfo fileInfo = new FileInfo(FilePath);
            string mimeType = "application/octet-stream";

            RegistryKey regKey = Registry.ClassesRoot.OpenSubKey(
                fileInfo.Extension.ToLower()
                );

            if (regKey != null)
            {
                object contentType = regKey.GetValue("Content Type");

                if (contentType != null)
                    mimeType = contentType.ToString();
            }

            return mimeType;
        }

        //클라에 보낼 헤더
        public void SendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, ref Socket mySocket)
        {
            String sBuffer = "";
            
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "application/octet-stream";  
            }
            sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";
            
            Byte[] bSendData = Encoding.UTF8.GetBytes(sBuffer);
            SendToBrowser(bSendData, ref mySocket);
            Console.WriteLine("파일 전체 크기 : " + iTotBytes.ToString());
        }
        public void SendToBrowser(String sData, ref Socket mySocket)
        {
            SendToBrowser(Encoding.UTF8.GetBytes(sData), ref mySocket);
        }
        public void SendToBrowser(Byte[] bSendData, ref Socket mySocket)
        {
            int numBytes = 0;
            try
            {
                
                if (mySocket.Connected)
                {
                    
                    if ((numBytes = mySocket.Send(bSendData, bSendData.Length, 0)) != -1)
                    {
                        Console.WriteLine("{0} Byte 전송", numBytes);
                    }
                }
                else
                    Console.WriteLine("연결 끊김");
            }
            catch (Exception e)
            {
                Console.WriteLine("오류 : {0} ", e);
            }
        }

        //수신시작
        public void StartListen()
        {
            int iStartPos = 0;
            string sRequest;
            string sDirName;
            string sRequestedFile;
            string sErrorMessage;
            string sLocalDir;
            string sPhysicalFilePath = "";
            string sFormattedMessage = "";
            string sResponse = "";
            string WebServerRoot = rootdir();
            
            while (true)
            {
                try
                {
                    
                    Socket mySocket = Listener.AcceptSocket();
                    if (mySocket.Connected)
                    {
                        Console.WriteLine("\n 연결 성공!\n==================\n 접속자 IP : {0}\n", mySocket.RemoteEndPoint);

                        
                        Byte[] bReceive = new Byte[1024];
                        
                        int i = mySocket.Receive(bReceive, bReceive.Length, 0);

                        
                        string sBuffer = Encoding.UTF8.GetString(bReceive);

                        if (sBuffer.Substring(0, 3) != "GET")
                        {
                            mySocket.Close();
                            continue;
                        }

                        iStartPos = sBuffer.IndexOf("HTTP", 1);

                        string sHttpVersion = sBuffer.Substring(iStartPos, 8);

                        sRequest = sBuffer.Substring(0, iStartPos - 1);

                        sRequest.Replace("\\", "/");

                        if ((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                        {
                            sRequest = sRequest + "/";
                        }
                        iStartPos = sRequest.LastIndexOf("/") + 1;
                        sRequestedFile = sRequest.Substring(iStartPos);

                        sDirName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 3);

                        sRequestedFile = System.Web.HttpUtility.UrlDecode(sRequestedFile);
                        
                        if (sRequestedFile == String.Empty)
                            sRequestedFile += "index.html"; //루트 경로로 들어오면 index.html 파일을 보여줌

                        if (sDirName == "/") 
                            sLocalDir = WebServerRoot;
                        else
                        {
                            sLocalDir = GetLocalPath(sDirName);
                        }

                        Console.WriteLine("요구한 폴더 : " + sLocalDir);
                        
                        if (sLocalDir.Length == 0)
                        {
                            sErrorMessage = "오류 : 해당 폴더가 존재하지 않음";
                            SendHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found", ref mySocket);

                            SendToBrowser(sErrorMessage, ref mySocket);
                            mySocket.Close();
                            continue;
                        }

                        String sMimeType = GetMimeType(sRequestedFile);

                        sPhysicalFilePath = sLocalDir + sRequestedFile;
                        Console.WriteLine("요구한 파일 : " + sPhysicalFilePath);

                        if (File.Exists(sPhysicalFilePath) == false)
                        {
                            sErrorMessage = "오류 :해당 파일이 존재하지 않음";
                            SendHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found", ref mySocket);
                            SendToBrowser(sErrorMessage, ref mySocket);
                            Console.WriteLine(sFormattedMessage);
                        }
                        else
                        {
                            int iTotBytes = 0;
                            sResponse = "";

                            FileStream fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read,
                              FileShare.Read);

                            BinaryReader reader = new BinaryReader(fs);
                            byte[] bytes = new byte[fs.Length];
                            int read;
                            while ((read = reader.Read(bytes, 0, bytes.Length)) != 0)
                            {
                                sResponse = sResponse + Encoding.UTF8.GetString(bytes, 0, read);
                                iTotBytes = iTotBytes + read;
                            }
                            reader.Close();
                            fs.Close();
                            
                            SendHeader(sHttpVersion, sMimeType, iTotBytes, " 200 OK", ref mySocket);
                            SendToBrowser(bytes, ref mySocket);
                        }
                        mySocket.Close();
                    }
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("오류 : 파일 없음");
                }
                catch (System.Net.Sockets.SocketException e)
                {

                    Console.WriteLine("소켓 오류 :" + e.ErrorCode);
                }
            }
        }





    }
}
