using System;
using System.Collections.Generic;

namespace NTUWorkPuncher
{
    public class CardRecord
    {
        public List<CardRecordItem> Items { get; set; }

        public CardRecord()
        {
            Items = new List<CardRecordItem>();
        }
    }

    public class CardRecordItem
    {
        public DateTime? PunchedIn => (string.IsNullOrEmpty(SignDateString) || string.IsNullOrEmpty(PunchedInString)) ?
            null : (DateTime?)DateTime.Parse($"{SignDateString} {PunchedInString}");

        public DateTime? PunchedOut => (string.IsNullOrEmpty(SignDateString) || string.IsNullOrEmpty(PunchedOutString)) ?
            null : (DateTime?)DateTime.Parse($"{SignDateString} {PunchedOutString}");

        public string SignDateString { get; set; }

        public string PunchedInString { get; set; }

        public string PunchedOutString { get; set; }
    }
}
