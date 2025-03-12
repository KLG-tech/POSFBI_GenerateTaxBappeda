using System;
using System.Collections.Generic;
using System.Text;

namespace GenerateTaxNew.Models
{
    public class CompanyData
    {
        public string company_code { get; set; }
        public string company_name { get; set; }
        public string transdate { get; set; }
        public string filePath { get; set; }
        public string ftpPath { get; set; }
        public string userFtp { get; set; }
        public string passFtp { get; set; }
        public string fileType { get; set; }
        public string Area { get; set; }
    }

    public class TransactionData
    {
        public string receive_no { get; set; }
        public string transdate { get; set; }
        public string transaction_code { get; set; }
        public decimal amount_exc_tax { get; set; }
        public decimal amount_inc_tax { get; set; }
        public decimal pb1 { get; set; }

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
}
