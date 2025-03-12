using System;
using System.Collections.Generic;
using System.Text;

namespace GenerateTaxNew.Models
{
    public class GenerateTax
    {
        public string Company_code { get; set; } = string.Empty;
        public DateTime? LastRun { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FtpPath { get; set; } = string.Empty;
        public string UserFtp { get; set; } = string.Empty;
        public string PassFtp { get; set; } = string.Empty;
    }
    public class TaxData
    {
        public string ReceiveNo { get; set; }
        public string WaktuString { get; set; }
        public string Waktu { get; set; }
        public decimal Amount { get; set; }
        public decimal Tax { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal Diskon { get; set; }
        public string DataTimestamp { get; set; }
        public List<ProductDetail> ProductDetails { get; set; }
    }

    public class ProductDetail
    {
        public string ProductName { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }

}
