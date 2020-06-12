using System;
using System.Net;

namespace NTUWorkPuncher
{
    public class AuthFailedException : Exception
    {
        public AuthFailedException(string Message = null) : base(Message) { }
    }

    public class EmptyResponseException : Exception
    {
        public EmptyResponseException(string Message = null) : base(Message) { }
    }

    public class APIFailedException : Exception
    {
        public HttpWebResponse Response { get; set; }
        public APIFailedException(HttpWebResponse Response) : base() { }
    }

    public class NotLoginedException : Exception
    {
        public NotLoginedException(string Message = null) : base(Message) { }
    }

    public class FetchCardsFailed : Exception
    {
        public FetchCardsFailed(string Message, Exception ex) : base(Message, innerException: ex) { }
    }
}
