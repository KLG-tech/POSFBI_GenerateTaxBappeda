using Dapper;
using GenerateTaxNew.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GenerateTaxNew.Service
{
    public class GenerateTaxService
    {
        private static IConfiguration Configuration { get; set; }
        private static ILogger<GenerateTaxService> Logger { get; set; }

        public GenerateTaxService(ILogger<GenerateTaxService> logger)
        {
            Logger = logger;
        }

        public async Task GenerateTaxFunctionAsync(string buCode, string connString, int retryCount)
        {
            Logger.LogInformation("==== Start Generate Data at " + DateTime.Now + " ====");
            var connectionString = connString;
            try
            {
                var sql = new StringBuilder();
                sql.Append(@"
                            SELECT 
                                b.company_code, 
                                b.company_name,
                                CONVERT(VARCHAR, DATEADD(DAY, 0, CAST(lastRun AS DATE)), 106) AS [transdate], 
                                filePath, 
                                ftpPath, 
                                userFtp, 
                                passFtp,
                                fileType,
                                a.area
                            FROM 
                                GenerateTaxConfig a 
                            INNER JOIN 
                                company_all b ON a.company_code = b.company_code 
                            Where a.company_code = '"+buCode+"' ");

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var companies = (await conn.QueryAsync<CompanyData>(sql.ToString())).ToList();

                    Logger.LogInformation($"Number of companies found: {companies.Count}");

                    var tasks = new List<Task>();

                    foreach (var company in companies)
                    {
                        //tasks.Add(Task.Run(async () =>
                        //{
                            try
                            {
                                Logger.LogInformation($"Processing company: {company.company_code}, Name: {company.company_name}");
                                var lastRunDate = await GetLastRunDate(conn, company.company_code, company.fileType);
                                Logger.LogInformation($"Last run date for {company.company_code}: {lastRunDate}");

                                List<TransactionData> transactions = new List<TransactionData>();

                                if (company.Area == "Bali")
                                {
                                    transactions = await GenerateTaxData(connectionString, company.company_code, lastRunDate, DateTime.Now);
                                }
                                else
                                {
                                    transactions = await GetTransactions(conn, company.company_code, lastRunDate, DateTime.Now);
                                }

                                Logger.LogInformation($"Number of transactions found for {company.company_code}: {transactions.Count}");

                                if (transactions.Count > 0)
                                {
                                    var csvData = GenerateCsv(transactions, company.Area);
                                    SaveToFile(csvData, company);

                                    Logger.LogInformation($"CSV file saved for {company.company_code}");

                                    // Upload to FTP
                                    bool ftpUploadSuccess = UploadFTP(company.ftpPath, company.filePath, $"{company.company_name}-{company.transdate}{company.fileType}", company.userFtp, company.passFtp, retryCount);
                                    //Logger.LogInformation($"Uploaded file to FTP for {company.company_code}");
                                    if (ftpUploadSuccess)
                                    {
                                        Logger.LogInformation($"Uploaded file to FTP for {company.company_code}");

                                        // Update lastRun only if FTP upload was successful
                                        UpdateLastRun(conn, company.company_code);
                                        Logger.LogInformation($"Updated last run for {company.company_code}");
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Failed to upload to FTP for {company.company_code}. Skipping last run update.");
                                    }
                                    // Update lastRun
                                    //UpdateLastRun(conn, company.company_code);
                                    //Logger.LogInformation($"Updated last run for {company.company_code}");
                                }
                                else
                                {
                                    Logger.LogWarning($"No data to generate for {company.company_code}...");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, $"An error occurred while processing company {company.company_code}.");
                            }
                        //}));
                    }

                    // Wait for all tasks to complete
                   // await Task.WhenAll(tasks);
                }

                Logger.LogInformation("Completed processing all companies.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred during processing.");
            }
            Logger.LogInformation("==== End Generate Data at " + DateTime.Now + " ====");
        }

        #region Comment Sequence Process
        //public void GenerateTaxFunction(string buCode,string connString,int RetryCount)
        //{
        //    Logger.LogInformation("==== Start Generate Data at "+DateTime.Now+" ====");
        //    var connectionString = connString;
        //    try
        //    {
        //        var sql = new StringBuilder();
        //        sql.Append(@"
        //        SELECT 
        //            b.company_code, 
        //            b.company_name,
        //            CONVERT(VARCHAR, DATEADD(DAY, 0, CAST(lastRun AS DATE)), 106) AS [transdate], 
        //            filePath, 
        //            ftpPath, 
        //            userFtp, 
        //            passFtp,
        //            fileType,
        //            a.area
        //        FROM 
        //            GenerateTaxConfig a 
        //        INNER JOIN 
        //            company_all b ON a.company_code = b.company_code ");
        //        //where b.company_code='I0' 

        //        using (var conn = new SqlConnection(connectionString))
        //        {
        //            conn.Open();
        //            var companies = conn.Query<CompanyData>(sql.ToString()).AsList();

        //            Logger.LogInformation($"Number of companies found: {companies.Count}");

        //            foreach (var company in companies)
        //            {
        //                Logger.LogInformation($"Processing company: {company.company_code}, Name: {company.company_name}");
        //                var lastRunDate = GetLastRunDate(conn, company.company_code, company.fileType);
        //                Logger.LogInformation($"Last run date for {company.company_code}: {lastRunDate}");
        //                List<TransactionData> transactions = new List<TransactionData>();
        //                if (company.Area == "Bali")
        //                {
        //                    transactions = GenerateTaxData(connectionString, company.company_code, lastRunDate, DateTime.Now);
        //                }
        //                else
        //                {
        //                    transactions = GetTransactions(conn, company.company_code, lastRunDate, DateTime.Now);
        //                }

        //                Logger.LogInformation($"Number of transactions found for {company.company_code}: {transactions.Count}");

        //                if (transactions.Count > 0)
        //                {
        //                    var csvData = GenerateCsv(transactions,company.Area);

        //                    SaveToFile(csvData, company);

        //                    Logger.LogInformation($"CSV file saved for {company.company_code}");

        //                    // Upload to FTP
        //                   UploadFTP(company.ftpPath, company.filePath, $"{company.company_name}-{company.transdate}{company.fileType}", company.userFtp, company.passFtp, RetryCount);
        //                    Logger.LogInformation($"Uploaded file to FTP for {company.company_code}");

        //                    // Update lastRun
        //                    UpdateLastRun(conn, company.company_code);
        //                    Logger.LogInformation($"Updated last run for {company.company_code}");
        //                }
        //                else
        //                {
        //                    //UpdateLastRun(conn, company.company_code);
        //                    Logger.LogWarning($"No data to generate for {company.company_code}...");
        //                }
        //            }
        //        }
        //        Logger.LogInformation("Completed processing all companies.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogError(ex, "An error occurred during processing.");
        //    }
        //    Logger.LogInformation("==== End Generate Data at " + DateTime.Now + " ====");
        //}
        #endregion

        private async Task<DateTime> GetLastRunDate(SqlConnection conn, string companyCode, string fileType)
        {
            var sql = "SELECT lastRun FROM GenerateTaxConfig WHERE company_code = @CompanyCode and fileType = @FileType";
            Logger.LogInformation($"Getting last run date for company code: {companyCode}");
            var res = conn.QuerySingle<DateTime>(sql, new { CompanyCode = companyCode, FileType = fileType });
            return res;
        }

        private async Task<List<TransactionData>> GetTransactions(SqlConnection conn, string companyCode, DateTime lastRunDate, DateTime tanggal)
        {
            //   var sql = @"
            //            Select * , tb.amount_inc_tax-tb.pb1 as [amount_ex_tax] from (
            //   SELECT 
            //                   receive_no,
            //                   CONVERT(VARCHAR, transdate, 103) + ' ' + CONVERT(VARCHAR, createtime, 108) AS [transdate],
            //                   amount AS [amount_inc_tax], 
            //                   amount*(ts.TAX_CODE/100/ts.RUMUS) pb1
            //               FROM 
            //                   receive r WITH (NOLOCK)
            //INNER JOIN tax_setup ts WITH (NOLOCK)
            //ON ts.SITE_CODE = r.company_code
            //               WHERE 
            //                   transdate = DATEADD(DAY, -1, CAST(@LastRunDate AS DATE)) 
            //                   AND company_code = @companyCode
            // AND ts.START_DATE <= @tanggal
            // AND ts.END_DATE >= @tanggal
            //                   AND complete = 1
            // ) as tb";

            var sql = @"SELECT 
                tb.company_code,
                CONVERT(VARCHAR, tb.transdate, 103) + ' ' + CONVERT(VARCHAR, tb.createtime, 108) AS [transdate],
                tb.receive_no,
                SUM(ROUND(tb.amount_exc_tax, 4)) AS amount_exc_tax,
                SUM(ROUND(tb.amount_inc_tax, 4)) AS amount_inc_tax,
                SUM(ROUND(tb.pb1, 4)) AS pb1
            FROM (
                SELECT 
                    r.company_code,
                    r.receive_no,
                    r.transdate,
                    r.createtime,
                    pm.tax_code,
                    SUM(
                        CASE 
                            WHEN c.disc > 0 THEN 
                                c.qty * (c.unitpriceasli - (c.unitpriceasli * c.disc / 100))  
                            ELSE 
                                c.qty * c.unitprice
                        END
                    ) AS amount_inc_tax,
                    SUM(
                        CASE 
                            WHEN c.disc > 0 THEN 
                                c.qty * (c.unitpriceasli - (c.unitpriceasli * c.disc / 100)) - 
                                (c.qty * (c.unitpriceasli - (c.unitpriceasli * c.disc / 100))) * (pm.tax_code / 100 / (1 + pm.tax_code / 100)) 
                            ELSE 
                                (c.qty * c.unitprice) - 
                                (c.qty * c.unitprice) * (pm.tax_code / 100 / (1 + pm.tax_code / 100)) 
                        END
                    ) AS amount_exc_tax,
                    SUM(
                        CASE 
                            WHEN c.disc > 0 THEN 
                                (c.qty * (c.unitpriceasli - (c.unitpriceasli * c.disc / 100))) * (pm.tax_code / 100 / (1 + pm.tax_code / 100)) 
                            ELSE 
                                (c.qty * c.unitprice) * (pm.tax_code / 100 / (1 + pm.tax_code / 100)) 
                        END
                    ) AS pb1
                FROM 
                    receive r with(nolock)
                INNER JOIN 
                    detailtrans c with(nolock) ON r.receive_no = c.receive_no
                INNER JOIN 
                    product_master pm with(nolock) ON pm.stockcode = c.stockcode
                WHERE 
                    r.company_code = @companyCode 
                    AND r.order_no NOT LIKE '%PHOENIX%'
                    AND r.custName NOT IN ('VMR')
                    AND r.posid NOT IN ('75', '76')
                    AND r.transdate = @lastRunDate
                GROUP BY 
                    r.company_code, r.receive_no, r.transdate, r.createtime, pm.tax_code
            ) AS tb
            WHERE tb.amount_exc_tax <> 0
            GROUP BY tb.company_code, tb.receive_no, tb.transdate, tb.createtime

            UNION ALL

            SELECT 
                tbl.company_code,
                CONVERT(VARCHAR, tbl.transdate, 103) + ' ' + CONVERT(VARCHAR, tbl.createtime, 108) AS [transdate],
                tbl.receive_no,
                ROUND(tbl.amount_exc_tax,4) AS amount_exc_tax,
                ROUND(tbl.amount_inc_tax, 4) AS amount_inc_tax,
                ROUND(tbl.pb1, 4) AS pb1 
            FROM (
                SELECT 
                    r.company_code,
                    r.receive_no,
                    r.transdate,
					r.createtime,
                    (r.amount / 1.1) AS amount_exc_tax,
                    r.amount AS amount_inc_tax,
                    ((r.amount / 1.1)*0.1) AS pb1
                FROM 
                    receive r with(nolock)
                INNER JOIN 
                    detailtrans c with(nolock) ON r.receive_no = c.receive_no
                WHERE 
                    r.company_code = @companyCode 
                    AND r.order_no LIKE '%PHOENIX%'
                    AND r.transdate = @lastRunDate
                GROUP BY 
                    r.company_code, 
                    r.receive_no, 
                    r.transdate, 
					r.createtime,
					r.amount)as tbl
            WHERE tbl.amount_exc_tax > 0;
            ";
            return conn.Query<TransactionData>(sql, new { CompanyCode = companyCode, LastRunDate = lastRunDate, tanggal = tanggal }).AsList();
        }

        public async Task<List<TransactionData>> GenerateTaxData(string _conn,string companyCode, DateTime lastRunDate, DateTime tanggal)
        {
            var taxDataList = new List<TransactionData>();

            try
            {
                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();

                    var companyData = conn.Query<dynamic>(
                        @"SELECT b.company_code, b.company_name, 
                             CONVERT(varchar, DATEADD(day, -1, CAST(lastRun AS date)), 106) AS [transdate]
                      FROM GenerateTaxConfig a 
                      INNER JOIN company_all b ON a.company_code = b.company_code 
                      WHERE a.company_code IN (@companyCode)", new { companyCode = companyCode }).AsList();

                    foreach (var company in companyData)
                    {
                        var resultInt = conn.ExecuteScalar<int>(
                            @"SELECT COUNT(1) 
                          FROM receive WITH(NOLOCK) 
                          WHERE transdate = (SELECT DATEADD(day, -1, CAST(lastRun AS date)) 
                                             FROM GenerateTaxConfig WHERE company_code = @CompanyCode) 
                          AND company_code = @CompanyCode",
                            new { CompanyCode = company.company_code });

                        var resultInt2 = conn.ExecuteScalar<int>(
                            @"SELECT countRows 
                          FROM dataImported WITH(NOLOCK) 
                          WHERE tableName = 'Receive' AND company = @CompanyCode 
                          AND transdate = DATEADD(day, -1, 
                              (SELECT lastRun FROM GenerateTaxConfig WHERE company_code = @CompanyCode))",
                            new { CompanyCode = company.company_code });

                        if (resultInt > 0)
                        {
                            var receiveData = conn.Query<TransactionData>(
                                @"SELECT receive_no, 
                                     CONVERT(varchar, transdate, 106) + ' ' + 
                                     LEFT(CAST(RIGHT(CAST(createtime AS varchar), 8) AS time), 5) AS [WaktuString], 
                                     CONVERT(varchar, transdate, 103) + ' ' + 
                                     LEFT(CAST(RIGHT(CAST(createtime AS varchar), 8) AS time), 8) + ' ' + '+0800' AS [Waktu], 
                                     Amount 
                              FROM receive WITH(NOLOCK) 
                              WHERE transdate = (SELECT DATEADD(day, -1, CAST(lastRun AS date)) 
                                                 FROM GenerateTaxConfig WHERE company_code = @CompanyCode) 
                              AND company_code = @CompanyCode AND complete = 1",
                                new { CompanyCode = company.company_code }).AsList();

                            foreach (var receive in receiveData)
                            {
                                var taxData = new TransactionData {
                                    ReceiveNo = receive.receive_no,
                                    WaktuString = receive.WaktuString,
                                    Waktu = receive.Waktu,
                                    Amount = receive.Amount,
                                    Tax = 0, // Update if needed
                                    ServiceCharge = 0, // Update if needed
                                    Diskon = 0, // Update if needed
                                    DataTimestamp = receive.Waktu, // Adjust if necessary
                                    ProductDetails = new List<ProductDetail>()
                                };

                                var detailData = conn.Query<ProductDetail>(
                                    @"SELECT product_name as ProductName, qty, 
                                         (CASE WHEN disc > 0 THEN (unitpriceasli - (unitpriceasli * disc / 100)) ELSE unitprice END) AS unitprice 
                                  FROM detailtrans WITH(NOLOCK) 
                                  WHERE receive_no = @ReceiveNo",
                                    new { ReceiveNo = receive.receive_no }).AsList();

                                taxData.ProductDetails.AddRange(detailData);
                                taxDataList.Add(taxData);
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exception (e.g., log it)
            }

            return taxDataList;
        }

        private string GenerateCsv(List<TransactionData> transactions,string area)
        {
            var csv = new StringBuilder();
            if (area == "Jaktim") { 
                csv.AppendLine("Receive No;transdate;transaction_code;amount_ex_tax;amount_inc_tax;pb1");
                var groupedTransactions = transactions
                    .GroupBy(t => t.receive_no)
                    .Select(g => new TransactionData {
                        receive_no = g.Key,
                        transdate = g.First().transdate,
                        transaction_code = g.First().transaction_code,
                        amount_exc_tax = g.Sum(t => t.amount_exc_tax),
                        amount_inc_tax = g.Sum(t => t.amount_inc_tax),
                        pb1 = g.Sum(t => t.pb1)
                    });
                foreach (var transaction in groupedTransactions)
                {
                    csv.AppendLine($"{transaction.receive_no};{transaction.transdate};{transaction.transaction_code};{Math.Round(transaction.amount_exc_tax, 4)};{transaction.amount_inc_tax};{Math.Round(transaction.pb1, 4)}");
                }
            }
            else
            {
                csv.AppendLine("Receive No;Waktu String;Waktu;Receive No;Amount;Tax;Service Charge;Diskon;Data Timestamp;Product Details");

                foreach (var transaction in transactions)
                {
                    var productDetails = string.Join("|", transaction.ProductDetails.Select(pd => $"{pd.ProductName}^{pd.Qty}^{Math.Round(pd.UnitPrice, 4)}"));

                    csv.AppendLine($"{transaction.ReceiveNo};{transaction.WaktuString};{transaction.Waktu};{transaction.ReceiveNo};{Math.Round(transaction.Amount, 4)};{transaction.Tax};{transaction.ServiceCharge};{transaction.Diskon};{transaction.DataTimestamp};{productDetails}");
                }

            }

            return csv.ToString();
        }

        private void SaveToFile(string csvData, CompanyData company)
        {
            string directoryPath = company.filePath;
            string compname = company.company_name.ToLower();
            string fileName = $"{company.company_name}-{company.transdate}{company.fileType}";
            string path = Path.Combine(directoryPath, fileName.Replace(" ",""));

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(path, csvData);
            Logger.LogInformation($"File saved at: {path}");
        }

        private void UpdateLastRun(SqlConnection conn, string companyCode)
        {
            var sql = "UPDATE GenerateTaxConfig SET lastRun = DATEADD(DAY, 1, lastRun) WHERE company_code = @CompanyCode";
            conn.Execute(sql, new { CompanyCode = companyCode });
            Logger.LogInformation($"Last run date updated for company: {companyCode}");
        }

        private bool UploadFTP(string ftpPath, string localFilePath, string fileName, string username, string password,int RetryCount)
            {
                var policy = Policy
                    .Handle<Exception>() // Retry on any exception
                    .WaitAndRetry(RetryCount, // Retry up to 3 times
                        attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Exponential backoff: 2^attempt seconds delay
                        (exception, timeSpan, attempt, context) => {
                            Logger.LogWarning($"Attempt {attempt} failed: {exception.Message}. Retrying in {timeSpan.TotalSeconds} seconds...");
                        });

                try
                {
                    string fullLocalFilePath = Path.Combine(localFilePath, fileName.Replace(" ", ""));

                    if (!File.Exists(fullLocalFilePath))
                    {
                        Logger.LogError($"File not found: {fullLocalFilePath}");
                        return false;
                    }

                    string uri = $"{ftpPath.TrimEnd('/')}/{fileName}";
                    Logger.LogInformation($"Connecting to FTP: {uri}");

                    policy.Execute(() =>
                    {
                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
                        request.Method = WebRequestMethods.Ftp.UploadFile;
                        request.Credentials = new NetworkCredential(username, password);
                        request.UseBinary = true;
                        request.ContentLength = new FileInfo(fullLocalFilePath).Length;

                        byte[] fileContents = File.ReadAllBytes(fullLocalFilePath);

                        using (var requestStream = request.GetRequestStream())
                        {
                            requestStream.Write(fileContents, 0, fileContents.Length);
                        }

                        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                        {
                            Logger.LogInformation($"Upload Complete for {fileName}, status: {response.StatusDescription}");
                        }
                    });
                return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Error uploading to FTP: {fileName}");
                return false;
                }
            }

    //private void UploadFTP(string ftpPath, string localFilePath, string fileName, string username, string password)
    //{
    //    try
    //    {
    //        string fullLocalFilePath = Path.Combine(localFilePath, fileName.Replace(" ", ""));

    //        if (!File.Exists(fullLocalFilePath))
    //        {
    //            Logger.LogError($"File not found: {fullLocalFilePath}");
    //            return;
    //        }

    //        string uri = $"{ftpPath.TrimEnd('/')}/{fileName}";
    //        Logger.LogInformation($"Connecting to FTP: {uri}");

    //        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
    //        request.Method = WebRequestMethods.Ftp.UploadFile;
    //        request.Credentials = new NetworkCredential(username, password);
    //        request.UseBinary = true;
    //        request.ContentLength = new FileInfo(fullLocalFilePath).Length;

    //        byte[] fileContents = File.ReadAllBytes(fullLocalFilePath);

    //        using (var requestStream = request.GetRequestStream())
    //        {
    //            requestStream.Write(fileContents, 0, fileContents.Length);
    //        }

    //        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
    //        {
    //            Logger.LogInformation($"Upload Complete for {fileName}, status: {response.StatusDescription}");
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Logger.LogError(ex, $"Error uploading to FTP: {fileName}");
    //    }
    //}
}
}
