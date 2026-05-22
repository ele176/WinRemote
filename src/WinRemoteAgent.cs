using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinRemoteAgent
{
    internal sealed class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var options = AgentOptions.Parse(args);
            if (options.CaptureTestPath != null)
            {
                try
                {
                    byte[] frame = ScreenTools.CaptureJpeg(options.StreamWidth, options.JpegQuality);
                    File.WriteAllBytes(options.CaptureTestPath, frame);
                    Console.WriteLine("Captured " + frame.Length.ToString(CultureInfo.InvariantCulture) + " bytes to " + options.CaptureTestPath);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Capture failed: " + ex.GetType().FullName + " - " + ex.Message);
                    return 2;
                }
            }

            var state = new AgentState(options);
            var server = new RemoteHttpServer(state);

            try
            {
                server.Start();
                PrintStartup(state);

                Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    server.Stop();
                };

                while (server.IsRunning)
                {
                    Thread.Sleep(250);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Agent failed: " + ex.Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void PrintStartup(AgentState state)
        {
            Console.WriteLine();
            Console.WriteLine("iOS Windows Remote Agent");
            Console.WriteLine("Listening on: " + state.Options.BindAddress + ":" + state.Options.Port);
            Console.WriteLine("Pair code: " + state.PairCode);
            Console.WriteLine();

            bool localOnly = NetworkTools.IsLoopbackBind(state.Options.BindAddress);
            if (localOnly)
            {
                Console.WriteLine("Local Agent URL used by the public HTTPS tunnel:");
            }
            else
            {
                Console.WriteLine("Open one of these URLs on your iPhone while it is on the same Wi-Fi:");
            }

            var addresses = NetworkTools.GetDisplayAddresses(state.Options.BindAddress);
            if (addresses.Count == 0)
            {
                Console.WriteLine("  http://127.0.0.1:" + state.Options.Port + "/?token=" + state.Token);
            }
            else
            {
                foreach (var address in addresses)
                {
                    Console.WriteLine("  http://" + address + ":" + state.Options.Port + "/?token=" + state.Token);
                }
            }

            Console.WriteLine();
            if (localOnly)
            {
                Console.WriteLine("For iPhone outside Wi-Fi, use the https://*.trycloudflare.com URL printed by run-public.bat.");
            }
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();
        }
    }

    internal sealed class AgentOptions
    {
        public string BindAddress = "0.0.0.0";
        public int Port = 17373;
        public int StreamFps = 8;
        public int StreamWidth = 1280;
        public long JpegQuality = 55;
        public string TokenOverride = null;
        public string PairCodeOverride = null;
        public string CaptureTestPath = null;

        public static AgentOptions Parse(string[] args)
        {
            var options = new AgentOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;

                if (arg == "--bind" && next != null)
                {
                    options.BindAddress = next;
                    i++;
                }
                else if (arg == "--port" && next != null)
                {
                    int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out options.Port);
                    i++;
                }
                else if (arg == "--fps" && next != null)
                {
                    int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out options.StreamFps);
                    i++;
                }
                else if (arg == "--width" && next != null)
                {
                    int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out options.StreamWidth);
                    i++;
                }
                else if (arg == "--quality" && next != null)
                {
                    long.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out options.JpegQuality);
                    i++;
                }
                else if (arg == "--token" && next != null)
                {
                    options.TokenOverride = next;
                    i++;
                }
                else if (arg == "--pair-code" && next != null)
                {
                    options.PairCodeOverride = next;
                    i++;
                }
                else if (arg == "--capture-test" && next != null)
                {
                    options.CaptureTestPath = next;
                    i++;
                }
            }

            if (options.Port < 1 || options.Port > 65535) options.Port = 17373;
            if (options.StreamFps < 1) options.StreamFps = 1;
            if (options.StreamFps > 20) options.StreamFps = 20;
            if (options.StreamWidth < 480) options.StreamWidth = 480;
            if (options.StreamWidth > 3840) options.StreamWidth = 3840;
            if (options.JpegQuality < 20) options.JpegQuality = 20;
            if (options.JpegQuality > 95) options.JpegQuality = 95;

            return options;
        }
    }

    internal sealed class AgentState
    {
        public AgentOptions Options { get; private set; }
        public string Token { get; private set; }
        public string PairCode { get; private set; }
        public string ContentRoot { get; private set; }
        private readonly object _pairLock = new object();
        private int _pairFailures;
        private DateTime _pairLockedUntilUtc = DateTime.MinValue;

        public AgentState(AgentOptions options)
        {
            Options = options;
            Token = String.IsNullOrEmpty(options.TokenOverride) ? SecurityTools.NewToken(32) : options.TokenOverride;
            PairCode = String.IsNullOrEmpty(options.PairCodeOverride) ? SecurityTools.NewPairCode() : options.PairCodeOverride;
            ContentRoot = ResolveContentRoot();
        }

        public bool IsAuthorized(HttpRequest request)
        {
            string value;

            if (request.Query.TryGetValue("token", out value) && SecurityTools.ConstantTimeEquals(value, Token))
            {
                return true;
            }

            if (request.Headers.TryGetValue("x-remote-token", out value) && SecurityTools.ConstantTimeEquals(value, Token))
            {
                return true;
            }

            if (request.Headers.TryGetValue("cookie", out value))
            {
                string cookieToken = HttpTools.FindCookie(value, "wr_token");
                if (cookieToken != null && SecurityTools.ConstantTimeEquals(cookieToken, Token))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryPair(string code, out string error)
        {
            lock (_pairLock)
            {
                DateTime now = DateTime.UtcNow;
                if (now < _pairLockedUntilUtc)
                {
                    int seconds = Math.Max(1, (int)Math.Ceiling((_pairLockedUntilUtc - now).TotalSeconds));
                    error = "locked:" + seconds.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                if (SecurityTools.ConstantTimeEquals(code.Trim(), PairCode))
                {
                    _pairFailures = 0;
                    error = null;
                    return true;
                }

                _pairFailures++;
                if (_pairFailures >= 5)
                {
                    _pairFailures = 0;
                    _pairLockedUntilUtc = now.AddSeconds(60);
                    error = "locked:60";
                    return false;
                }

                error = "bad_code";
                return false;
            }
        }

        private static string ResolveContentRoot()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string parent = Path.GetFullPath(Path.Combine(baseDir, ".."));
            if (Directory.Exists(Path.Combine(parent, "public")))
            {
                return parent;
            }

            string current = Environment.CurrentDirectory;
            if (Directory.Exists(Path.Combine(current, "public")))
            {
                return current;
            }

            return baseDir;
        }
    }

    internal sealed class RemoteHttpServer
    {
        private readonly AgentState _state;
        private TcpListener _listener;
        private volatile bool _running;

        public bool IsRunning { get { return _running; } }

        public RemoteHttpServer(AgentState state)
        {
            _state = state;
        }

        public void Start()
        {
            IPAddress address;
            if (!IPAddress.TryParse(_state.Options.BindAddress, out address))
            {
                address = IPAddress.Any;
            }

            _listener = new TcpListener(address, _state.Options.Port);
            _listener.Start();
            _running = true;

            var thread = new Thread(AcceptLoop);
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_listener != null) _listener.Stop();
            }
            catch
            {
            }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
                }
                catch
                {
                    if (_running)
                    {
                        Thread.Sleep(200);
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                client.NoDelay = true;

                try
                {
                    NetworkStream stream = client.GetStream();
                    stream.ReadTimeout = 15000;
                    HttpRequest request = HttpTools.ReadRequest(stream);
                    if (request == null)
                    {
                        return;
                    }

                    Route(stream, request);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " request failed: " + ex);
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        HttpTools.WriteText(stream, 500, "text/plain; charset=utf-8", "Internal server error");
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Route(NetworkStream stream, HttpRequest request)
        {
            string path = request.Path;

            if (request.Method == "GET" && path == "/")
            {
                WriteStaticFile(stream, "index.html", "text/html; charset=utf-8");
                return;
            }

            if (request.Method == "GET" && path == "/styles.css")
            {
                WriteStaticFile(stream, "styles.css", "text/css; charset=utf-8");
                return;
            }

            if (request.Method == "GET" && path == "/app.js")
            {
                WriteStaticFile(stream, "app.js", "application/javascript; charset=utf-8");
                return;
            }

            if (request.Method == "GET" && path == "/favicon.ico")
            {
                HttpTools.WriteBytes(stream, 204, "image/x-icon", new byte[0], (Dictionary<string, string>)null);
                return;
            }

            if (request.Method == "POST" && path == "/api/pair")
            {
                HandlePair(stream, request);
                return;
            }

            if (!_state.IsAuthorized(request))
            {
                HttpTools.WriteJson(stream, 401, "{\"ok\":false,\"error\":\"unauthorized\"}", null);
                return;
            }

            if (request.Method == "GET" && path == "/api/status")
            {
                HandleStatus(stream);
                return;
            }

            if (request.Method == "GET" && path == "/api/screen.jpg")
            {
                HandleScreen(stream, request);
                return;
            }

            if (request.Method == "GET" && path == "/stream.mjpg")
            {
                HandleStream(stream, request);
                return;
            }

            if (request.Method == "POST" && path == "/api/input")
            {
                HandleInput(stream, request);
                return;
            }

            HttpTools.WriteText(stream, 404, "text/plain; charset=utf-8", "Not found");
        }

        private void WriteStaticFile(NetworkStream stream, string fileName, string contentType)
        {
            string path = Path.Combine(_state.ContentRoot, "public", fileName);
            if (!File.Exists(path))
            {
                HttpTools.WriteText(stream, 404, "text/plain; charset=utf-8", "Missing " + fileName);
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            HttpTools.WriteBytes(stream, 200, contentType, bytes, "no-store");
        }

        private void HandlePair(NetworkStream stream, HttpRequest request)
        {
            Dictionary<string, string> form = HttpTools.ParseForm(request.BodyText);
            string code;
            string pairError = null;
            if (form.TryGetValue("code", out code) && _state.TryPair(code, out pairError))
            {
                string token = JsonTools.Escape(_state.Token);
                HttpTools.WriteJson(stream, 200, "{\"ok\":true,\"token\":\"" + token + "\"}", "wr_token=" + Uri.EscapeDataString(_state.Token) + "; Path=/; SameSite=Strict");
                return;
            }

            if (pairError == null)
            {
                pairError = "missing_code";
            }

            HttpTools.WriteJson(stream, 403, "{\"ok\":false,\"error\":\"" + JsonTools.Escape(pairError) + "\"}", null);
        }

        private void HandleStatus(NetworkStream stream)
        {
            Rectangle bounds = ScreenTools.VirtualBounds;
            string json = "{"
                + "\"ok\":true,"
                + "\"screen\":{\"left\":" + bounds.Left + ",\"top\":" + bounds.Top + ",\"width\":" + bounds.Width + ",\"height\":" + bounds.Height + "},"
                + "\"fps\":" + _state.Options.StreamFps + ","
                + "\"quality\":" + _state.Options.JpegQuality
                + "}";

            HttpTools.WriteJson(stream, 200, json, null);
        }

        private void HandleScreen(NetworkStream stream, HttpRequest request)
        {
            int width = HttpTools.QueryInt(request.Query, "width", _state.Options.StreamWidth, 320, 3840);
            long quality = HttpTools.QueryLong(request.Query, "quality", _state.Options.JpegQuality, 20, 95);
            byte[] frame = ScreenTools.CaptureJpeg(width, quality);

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            HttpTools.WriteBytes(stream, 200, "image/jpeg", frame, headers);
        }

        private void HandleStream(NetworkStream stream, HttpRequest request)
        {
            int fps = HttpTools.QueryInt(request.Query, "fps", _state.Options.StreamFps, 1, 20);
            int width = HttpTools.QueryInt(request.Query, "width", _state.Options.StreamWidth, 320, 3840);
            long quality = HttpTools.QueryLong(request.Query, "quality", _state.Options.JpegQuality, 20, 95);
            int delay = Math.Max(50, 1000 / fps);
            string boundary = "wrframe";

            string headers = "HTTP/1.1 200 OK\r\n"
                + "Content-Type: multipart/x-mixed-replace; boundary=" + boundary + "\r\n"
                + "Cache-Control: no-store, no-cache, must-revalidate\r\n"
                + "Connection: close\r\n"
                + "\r\n";
            HttpTools.WriteAscii(stream, headers);

            while (_running)
            {
                byte[] frame = ScreenTools.CaptureJpeg(width, quality);
                string part = "--" + boundary + "\r\n"
                    + "Content-Type: image/jpeg\r\n"
                    + "Content-Length: " + frame.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
                    + "\r\n";

                HttpTools.WriteAscii(stream, part);
                stream.Write(frame, 0, frame.Length);
                HttpTools.WriteAscii(stream, "\r\n");
                stream.Flush();
                Thread.Sleep(delay);
            }
        }

        private void HandleInput(NetworkStream stream, HttpRequest request)
        {
            Dictionary<string, string> form = HttpTools.ParseForm(request.BodyText);
            string error = RemoteInput.Handle(form);

            if (error == null)
            {
                HttpTools.WriteJson(stream, 200, "{\"ok\":true}", null);
            }
            else
            {
                HttpTools.WriteJson(stream, 400, "{\"ok\":false,\"error\":\"" + JsonTools.Escape(error) + "\"}", null);
            }
        }
    }

    internal sealed class HttpRequest
    {
        public string Method;
        public string Target;
        public string Path;
        public Dictionary<string, string> Query;
        public Dictionary<string, string> Headers;
        public byte[] Body;

        public string BodyText
        {
            get { return Body == null ? "" : Encoding.UTF8.GetString(Body); }
        }
    }

    internal static class HttpTools
    {
        public static HttpRequest ReadRequest(NetworkStream stream)
        {
            byte[] headerBytes = ReadUntilHeaderEnd(stream, 65536);
            if (headerBytes == null || headerBytes.Length == 0)
            {
                return null;
            }

            string headerText = Encoding.ASCII.GetString(headerBytes);
            string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return null;
            }

            string[] first = lines[0].Split(' ');
            if (first.Length < 2)
            {
                return null;
            }

            var request = new HttpRequest();
            request.Method = first[0].Trim().ToUpperInvariant();
            request.Target = first[1].Trim();
            request.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            request.Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim();
                request.Headers[key] = value;
            }

            ParseTarget(request.Target, out request.Path, request.Query);

            int length = 0;
            string lengthValue;
            if (request.Headers.TryGetValue("content-length", out lengthValue))
            {
                int.TryParse(lengthValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out length);
            }

            request.Body = length > 0 ? ReadExact(stream, length) : new byte[0];
            return request;
        }

        public static void WriteText(NetworkStream stream, int status, string contentType, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            WriteBytes(stream, status, contentType, bytes, (Dictionary<string, string>)null);
        }

        public static void WriteJson(NetworkStream stream, int status, string json, string setCookie)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Cache-Control"] = "no-store";
            if (setCookie != null)
            {
                headers["Set-Cookie"] = setCookie;
            }

            WriteBytes(stream, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json), headers);
        }

        public static void WriteBytes(NetworkStream stream, int status, string contentType, byte[] bytes, string cacheControl)
        {
            Dictionary<string, string> headers = null;
            if (cacheControl != null)
            {
                headers = new Dictionary<string, string>();
                headers["Cache-Control"] = cacheControl;
            }

            WriteBytes(stream, status, contentType, bytes, headers);
        }

        public static void WriteBytes(NetworkStream stream, int status, string contentType, byte[] bytes, Dictionary<string, string> extraHeaders)
        {
            string reason = ReasonPhrase(status);
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            builder.Append("Content-Type: ").Append(contentType).Append("\r\n");
            builder.Append("Content-Length: ").Append(bytes.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            builder.Append("Connection: close\r\n");
            builder.Append("X-Content-Type-Options: nosniff\r\n");
            builder.Append("X-Frame-Options: DENY\r\n");
            builder.Append("Referrer-Policy: no-referrer\r\n");
            builder.Append("Content-Security-Policy: default-src 'self'; img-src 'self' data: blob:; style-src 'self'; script-src 'self'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'\r\n");

            if (extraHeaders != null)
            {
                foreach (var pair in extraHeaders)
                {
                    builder.Append(pair.Key).Append(": ").Append(pair.Value).Append("\r\n");
                }
            }

            builder.Append("\r\n");
            WriteAscii(stream, builder.ToString());
            if (bytes.Length > 0)
            {
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.Flush();
        }

        public static void WriteAscii(NetworkStream stream, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static Dictionary<string, string> ParseForm(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (body == null) return result;

            string[] parts = body.Split('&');
            foreach (string part in parts)
            {
                if (part.Length == 0) continue;
                int equals = part.IndexOf('=');
                string rawKey = equals >= 0 ? part.Substring(0, equals) : part;
                string rawValue = equals >= 0 ? part.Substring(equals + 1) : "";
                string key = UrlDecode(rawKey);
                string value = UrlDecode(rawValue);
                result[key] = value;
            }

            return result;
        }

        public static int QueryInt(Dictionary<string, string> query, string key, int fallback, int min, int max)
        {
            string value;
            int parsed;
            if (!query.TryGetValue(key, out value) || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return fallback;
            }

            if (parsed < min) return min;
            if (parsed > max) return max;
            return parsed;
        }

        public static long QueryLong(Dictionary<string, string> query, string key, long fallback, long min, long max)
        {
            string value;
            long parsed;
            if (!query.TryGetValue(key, out value) || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return fallback;
            }

            if (parsed < min) return min;
            if (parsed > max) return max;
            return parsed;
        }

        public static string FindCookie(string cookieHeader, string name)
        {
            if (cookieHeader == null) return null;

            string[] parts = cookieHeader.Split(';');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                int equals = trimmed.IndexOf('=');
                if (equals <= 0) continue;

                string key = trimmed.Substring(0, equals).Trim();
                string value = trimmed.Substring(equals + 1).Trim();
                if (StringComparer.OrdinalIgnoreCase.Equals(key, name))
                {
                    return UrlDecode(value);
                }
            }

            return null;
        }

        private static byte[] ReadUntilHeaderEnd(NetworkStream stream, int maxBytes)
        {
            var buffer = new MemoryStream();
            int matched = 0;
            int[] marker = new int[] { 13, 10, 13, 10 };

            while (buffer.Length < maxBytes)
            {
                int b = stream.ReadByte();
                if (b < 0)
                {
                    break;
                }

                buffer.WriteByte((byte)b);

                if (b == marker[matched])
                {
                    matched++;
                    if (matched == marker.Length)
                    {
                        byte[] all = buffer.ToArray();
                        byte[] header = new byte[all.Length - marker.Length];
                        Buffer.BlockCopy(all, 0, header, 0, header.Length);
                        return header;
                    }
                }
                else
                {
                    matched = b == marker[0] ? 1 : 0;
                }
            }

            return buffer.ToArray();
        }

        private static byte[] ReadExact(NetworkStream stream, int length)
        {
            byte[] body = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(body, offset, length - offset);
                if (read <= 0)
                {
                    break;
                }
                offset += read;
            }

            if (offset == length)
            {
                return body;
            }

            byte[] partial = new byte[offset];
            Buffer.BlockCopy(body, 0, partial, 0, offset);
            return partial;
        }

        private static void ParseTarget(string target, out string path, Dictionary<string, string> query)
        {
            int question = target.IndexOf('?');
            path = question >= 0 ? target.Substring(0, question) : target;
            string queryText = question >= 0 ? target.Substring(question + 1) : "";

            if (path.Length == 0) path = "/";
            query.Clear();

            string[] parts = queryText.Split('&');
            foreach (string part in parts)
            {
                if (part.Length == 0) continue;
                int equals = part.IndexOf('=');
                string rawKey = equals >= 0 ? part.Substring(0, equals) : part;
                string rawValue = equals >= 0 ? part.Substring(equals + 1) : "";
                query[UrlDecode(rawKey)] = UrlDecode(rawValue);
            }
        }

        private static string UrlDecode(string value)
        {
            if (value == null) return "";

            value = value.Replace("+", " ");
            var bytes = new List<byte>();
            var output = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '%' && i + 2 < value.Length)
                {
                    int hi = Hex(value[i + 1]);
                    int lo = Hex(value[i + 2]);
                    if (hi >= 0 && lo >= 0)
                    {
                        bytes.Add((byte)((hi << 4) + lo));
                        i += 2;
                        continue;
                    }
                }

                if (bytes.Count > 0)
                {
                    output.Append(Encoding.UTF8.GetString(bytes.ToArray()));
                    bytes.Clear();
                }

                output.Append(c);
            }

            if (bytes.Count > 0)
            {
                output.Append(Encoding.UTF8.GetString(bytes.ToArray()));
            }

            return output.ToString();
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static string ReasonPhrase(int status)
        {
            if (status == 200) return "OK";
            if (status == 204) return "No Content";
            if (status == 400) return "Bad Request";
            if (status == 401) return "Unauthorized";
            if (status == 403) return "Forbidden";
            if (status == 404) return "Not Found";
            if (status == 500) return "Internal Server Error";
            return "OK";
        }
    }

    internal static class ScreenTools
    {
        public static Rectangle VirtualBounds
        {
            get { return SystemInformation.VirtualScreen; }
        }

        public static byte[] CaptureJpeg(int targetWidth, long quality)
        {
            try
            {
                return CaptureDesktopJpeg(targetWidth, quality);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " capture failed: " + ex.GetType().FullName + " - " + ex.Message);
                return ErrorJpeg(targetWidth, quality, "Screen capture unavailable");
            }
        }

        private static byte[] CaptureDesktopJpeg(int targetWidth, long quality)
        {
            Rectangle bounds = VirtualBounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                Bitmap output = bitmap;
                Bitmap resized = null;

                if (targetWidth > 0 && targetWidth < bitmap.Width)
                {
                    int targetHeight = Math.Max(1, (int)Math.Round((double)bitmap.Height * targetWidth / bitmap.Width));
                    resized = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
                    using (Graphics graphics = Graphics.FromImage(resized))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.InterpolationMode = InterpolationMode.Low;
                        graphics.SmoothingMode = SmoothingMode.None;
                        graphics.PixelOffsetMode = PixelOffsetMode.Half;
                        graphics.DrawImage(bitmap, new Rectangle(0, 0, targetWidth, targetHeight));
                    }
                    output = resized;
                }

                try
                {
                    using (var ms = new MemoryStream())
                    {
                        ImageCodecInfo encoder = FindJpegEncoder();
                        if (encoder != null)
                        {
                            var parameters = new EncoderParameters(1);
                            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                            output.Save(ms, encoder, parameters);
                        }
                        else
                        {
                            output.Save(ms, ImageFormat.Jpeg);
                        }

                        return ms.ToArray();
                    }
                }
                finally
                {
                    if (resized != null)
                    {
                        resized.Dispose();
                    }
                }
            }
        }

        private static byte[] ErrorJpeg(int targetWidth, long quality, string message)
        {
            int width = targetWidth < 480 ? 640 : targetWidth;
            int height = Math.Max(270, (int)Math.Round(width * 9.0 / 16.0));
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(17, 19, 21));
                    using (var pen = new Pen(Color.FromArgb(52, 58, 61), 2))
                    {
                        graphics.DrawRectangle(pen, 18, 18, width - 36, height - 36);
                    }

                    using (var titleFont = new Font("Segoe UI", 22, FontStyle.Bold))
                    using (var bodyFont = new Font("Segoe UI", 12, FontStyle.Regular))
                    using (var titleBrush = new SolidBrush(Color.FromArgb(244, 241, 234)))
                    using (var bodyBrush = new SolidBrush(Color.FromArgb(168, 174, 169)))
                    {
                        graphics.DrawString("WinRemote", titleFont, titleBrush, 34, 32);
                        graphics.DrawString(message, bodyFont, bodyBrush, 36, 78);
                        graphics.DrawString("Run the agent in an unlocked interactive Windows desktop.", bodyFont, bodyBrush, 36, 104);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    ImageCodecInfo encoder = FindJpegEncoder();
                    if (encoder != null)
                    {
                        var parameters = new EncoderParameters(1);
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                        bitmap.Save(ms, encoder, parameters);
                    }
                    else
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                    }

                    return ms.ToArray();
                }
            }
        }

        private static ImageCodecInfo FindJpegEncoder()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                {
                    return codecs[i];
                }
            }

            return null;
        }
    }

    internal static class RemoteInput
    {
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const int INPUT_KEYBOARD = 1;

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        public static string Handle(Dictionary<string, string> form)
        {
            string action;
            if (!form.TryGetValue("action", out action))
            {
                return "missing_action";
            }

            action = action.ToLowerInvariant();

            if (action == "move" || action == "down" || action == "up" || action == "click" || action == "doubleclick" || action == "rightclick")
            {
                double x;
                double y;
                if (!TryNormalized(form, "x", out x) || !TryNormalized(form, "y", out y))
                {
                    return "bad_coordinates";
                }

                MoveToNormalized(x, y);

                if (action == "down")
                {
                    MouseDown(Button(form));
                }
                else if (action == "up")
                {
                    MouseUp(Button(form));
                }
                else if (action == "click")
                {
                    Click(Button(form));
                }
                else if (action == "doubleclick")
                {
                    Click("left");
                    Thread.Sleep(80);
                    Click("left");
                }
                else if (action == "rightclick")
                {
                    Click("right");
                }

                return null;
            }

            if (action == "wheel")
            {
                int delta = IntValue(form, "delta", 0);
                if (delta == 0)
                {
                    return "bad_delta";
                }
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, UIntPtr.Zero);
                return null;
            }

            if (action == "text")
            {
                string text;
                if (!form.TryGetValue("text", out text))
                {
                    return "missing_text";
                }

                SendUnicodeText(text);
                return null;
            }

            if (action == "key")
            {
                string key;
                if (!form.TryGetValue("key", out key))
                {
                    return "missing_key";
                }

                ushort virtualKey;
                if (!TryMapKey(key, out virtualKey))
                {
                    return "unknown_key";
                }

                SendVirtualKey(virtualKey);
                return null;
            }

            return "unknown_action";
        }

        private static string Button(Dictionary<string, string> form)
        {
            string button;
            if (!form.TryGetValue("button", out button))
            {
                return "left";
            }

            button = button.ToLowerInvariant();
            return button == "right" ? "right" : "left";
        }

        private static void MoveToNormalized(double x, double y)
        {
            Rectangle bounds = ScreenTools.VirtualBounds;
            int px = bounds.Left + (int)Math.Round((bounds.Width - 1) * Clamp01(x));
            int py = bounds.Top + (int)Math.Round((bounds.Height - 1) * Clamp01(y));
            SetCursorPos(px, py);
        }

        private static void Click(string button)
        {
            MouseDown(button);
            Thread.Sleep(40);
            MouseUp(button);
        }

        private static void MouseDown(string button)
        {
            mouse_event(button == "right" ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        }

        private static void MouseUp(string button)
        {
            mouse_event(button == "right" ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private static void SendUnicodeText(string text)
        {
            if (text == null) return;

            foreach (char ch in text)
            {
                SendUnicodeChar(ch);
            }
        }

        private static void SendUnicodeChar(char ch)
        {
            INPUT down = new INPUT();
            down.type = INPUT_KEYBOARD;
            down.u.ki.wScan = ch;
            down.u.ki.dwFlags = KEYEVENTF_UNICODE;

            INPUT up = down;
            up.u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

            INPUT[] inputs = new INPUT[] { down, up };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void SendVirtualKey(ushort virtualKey)
        {
            INPUT down = new INPUT();
            down.type = INPUT_KEYBOARD;
            down.u.ki.wVk = virtualKey;

            INPUT up = down;
            up.u.ki.dwFlags = KEYEVENTF_KEYUP;

            INPUT[] inputs = new INPUT[] { down, up };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static bool TryMapKey(string key, out ushort virtualKey)
        {
            switch (key)
            {
                case "Backspace": virtualKey = 0x08; return true;
                case "Tab": virtualKey = 0x09; return true;
                case "Enter": virtualKey = 0x0D; return true;
                case "Escape": virtualKey = 0x1B; return true;
                case "Delete": virtualKey = 0x2E; return true;
                case "ArrowLeft": virtualKey = 0x25; return true;
                case "ArrowUp": virtualKey = 0x26; return true;
                case "ArrowRight": virtualKey = 0x27; return true;
                case "ArrowDown": virtualKey = 0x28; return true;
                case "Home": virtualKey = 0x24; return true;
                case "End": virtualKey = 0x23; return true;
                case "PageUp": virtualKey = 0x21; return true;
                case "PageDown": virtualKey = 0x22; return true;
                default:
                    virtualKey = 0;
                    return false;
            }
        }

        private static bool TryNormalized(Dictionary<string, string> form, string key, out double value)
        {
            string raw;
            if (!form.TryGetValue(key, out raw))
            {
                value = 0;
                return false;
            }

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static int IntValue(Dictionary<string, string> form, string key, int fallback)
        {
            string raw;
            int value;
            if (!form.TryGetValue(key, out raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }

            return value;
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }

    internal static class SecurityTools
    {
        public static string NewToken(int bytes)
        {
            byte[] buffer = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }

            return Convert.ToBase64String(buffer).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public static string NewPairCode()
        {
            byte[] buffer = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }

            uint number = BitConverter.ToUInt32(buffer, 0);
            number = 10000000u + (number % 90000000u);
            return number.ToString(CultureInfo.InvariantCulture);
        }

        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;

            byte[] aa = Encoding.UTF8.GetBytes(a);
            byte[] bb = Encoding.UTF8.GetBytes(b);
            int diff = aa.Length ^ bb.Length;
            int max = Math.Max(aa.Length, bb.Length);

            for (int i = 0; i < max; i++)
            {
                byte av = i < aa.Length ? aa[i] : (byte)0;
                byte bv = i < bb.Length ? bb[i] : (byte)0;
                diff |= av ^ bv;
            }

            return diff == 0;
        }
    }

    internal static class NetworkTools
    {
        public static List<string> GetLocalIPv4Addresses()
        {
            var result = new List<string>();

            try
            {
                IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(address)) continue;
                    string text = address.ToString();
                    if (!result.Contains(text))
                    {
                        result.Add(text);
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        public static List<string> GetDisplayAddresses(string bindAddress)
        {
            var result = new List<string>();
            IPAddress parsed;

            if (IPAddress.TryParse(bindAddress, out parsed))
            {
                if (IPAddress.IsLoopback(parsed))
                {
                    result.Add("127.0.0.1");
                    return result;
                }

                if (!parsed.Equals(IPAddress.Any) && !parsed.Equals(IPAddress.IPv6Any))
                {
                    result.Add(parsed.ToString());
                    return result;
                }
            }

            result = GetLocalIPv4Addresses();
            if (result.Count == 0)
            {
                result.Add("127.0.0.1");
            }

            return result;
        }

        public static bool IsLoopbackBind(string bindAddress)
        {
            IPAddress parsed;
            if (!IPAddress.TryParse(bindAddress, out parsed))
            {
                return false;
            }

            return IPAddress.IsLoopback(parsed);
        }
    }

    internal static class JsonTools
    {
        public static string Escape(string value)
        {
            if (value == null) return "";

            var builder = new StringBuilder();
            foreach (char ch in value)
            {
                if (ch == '\\') builder.Append("\\\\");
                else if (ch == '"') builder.Append("\\\"");
                else if (ch == '\n') builder.Append("\\n");
                else if (ch == '\r') builder.Append("\\r");
                else if (ch == '\t') builder.Append("\\t");
                else if (ch < 32) builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                else builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
