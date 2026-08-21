using System;
using System.IO;
using System.Text;
using System.Web;

namespace XYHMember
{
    /// <summary>
    /// 会话守卫响应过滤器：把 sessionGuard.js 注入到每个 HTML 页面。
    /// 当会话过期后 AuthFilter 对异步请求返回 401 时，由前端 sessionGuard.js 整页跳转登录。
    /// 非 HTML 响应（JSON / 文件下载等，即不含 &lt;/body&gt;）原样透传。
    /// </summary>
    public class SessionGuardHtmlFilter : Stream
    {
        private readonly Stream _sink;
        private readonly MemoryStream _buffer = new MemoryStream();
        private readonly Encoding _encoding;
        private readonly string _guardHtml;

        private SessionGuardHtmlFilter(Stream sink, Encoding encoding, string guardHtml)
        {
            _sink = sink;
            _encoding = encoding ?? Encoding.UTF8;
            _guardHtml = guardHtml;
        }

        public static SessionGuardHtmlFilter Create(Stream sink, Encoding encoding)
        {
            return new SessionGuardHtmlFilter(sink, encoding, BuildGuardHtml());
        }

        private static string BuildGuardHtml()
        {
            var appPath = HttpContext.Current?.Request.ApplicationPath ?? "/";
            var root = appPath == "/" ? "" : appPath.TrimEnd('/');
            var loginUrl = root + "/Home/Login";
            return "<script>window.__LOGIN_URL__='" + loginUrl + "';</script>" +
                   "<script src='" + root + "/Scripts/sessionGuard.js'></script>";
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _buffer.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            var bytes = _buffer.ToArray();
            var html = _encoding.GetString(bytes);
            var idx = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(_guardHtml) && idx >= 0)
            {
                html = html.Substring(0, idx) + _guardHtml + html.Substring(idx);
                bytes = _encoding.GetBytes(html);
            }
            _sink.Write(bytes, 0, bytes.Length);
            _sink.Flush();
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
