using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Data;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.RegularExpressions;

namespace CSVtoSQL
{
    public class Function1
    {
        private readonly IConfiguration _configuration;

        public Function1(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("processFile")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                //string name = req.Query["name"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<RequestBody>(requestBody);
                // = name ?? data?.name;

                var tableCSV = CreateDataTable(data.ColumnMappings);
                var file = await GetBlob(data.FileLocation, data.FileName);
                var tblInsert = await ReadBlobAsync(file, tableCSV);
                await InsertCSVRecords(data.TableName, tblInsert, data.ColumnMappings);

                /* string responseMessage = string.IsNullOrEmpty(name)
                     ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                     : $"Hello, {name}. This HTTP triggered function executed successfully.";*/

                string responseMessage = $"File has been processed successfully";
                var responseBody = new ResponseBody
                {
                    HttpResponseCode = (int)HttpStatusCode.OK,
                    Message = responseMessage
                };

                return new OkObjectResult(JsonConvert.SerializeObject(responseBody));
            }
            catch (Exception ex)
            {
                var errMsg = ex.Message + "\n" + ex.StackTrace;
                var responseBody = new ResponseBody
                {
                    HttpResponseCode = (int)HttpStatusCode.InternalServerError,
                    Message = errMsg
                };

                return new ObjectResult(JsonConvert.SerializeObject(responseBody)) 
                { 
                    StatusCode = (int)HttpStatusCode.InternalServerError 
                };
            }
        }

        private async Task InsertCSVRecords(string tableName, DataTable csvRecords, List<ColumnMapping> columnMappings)
        {
            string connString = _configuration["SqlConnectionStringMI"];

            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.AccessToken = await (new Microsoft.Azure.Services.AppAuthentication.AzureServiceTokenProvider()).GetAccessTokenAsync("https://database.windows.net/");
                sqlConnection.Open();

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(sqlConnection);

                bulkCopy.DestinationTableName = tableName;
                foreach (var mapping in columnMappings)
                {
                    bulkCopy.ColumnMappings.Add(mapping.Source, mapping.Destination);
                }

                await bulkCopy.WriteToServerAsync(csvRecords);
            }
        }

        private async Task<BlobDownloadInfo> GetBlob(string fileLocation, string filename)
        {
            var connString = _configuration["StorageConnectionString"];
            BlobServiceClient blobServiceClient = new BlobServiceClient(connString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(fileLocation);
            BlobClient blobClient = containerClient.GetBlobClient(filename);
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            return download;
        }

        private async Task<DataTable> ReadBlobAsync(BlobDownloadInfo file, DataTable tblcsv)
        {
            Regex CSVParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
            int rowCount = 0;
            
            using (var streamReader = new StreamReader(file.Content))
            {
                while (!streamReader.EndOfStream)
                {
                    var line = await streamReader.ReadLineAsync();
                    if (rowCount == 0)
                    {
                        rowCount++;
                        continue;
                    }

                    int colCount = 0;
                    tblcsv.Rows.Add();

                    //foreach (string column in line.Split(','))
                    foreach (string column in CSVParser.Split(line))
                    {
                        tblcsv.Rows[tblcsv.Rows.Count - 1][colCount] = tblcsv.Columns[colCount].ColumnName == "ISN" ? rowCount.ToString() : column;                    
                        colCount++;
                    }

                    rowCount++;
                    //Console.WriteLine(line);
                }
            }

            return tblcsv;
        }

        private DataTable CreateDataTable(List<ColumnMapping> columnMappings)
        {
            DataTable tblcsv = new DataTable();
            //creating columns
            foreach (var mapping in columnMappings)
            {
                tblcsv.Columns.Add(mapping.Source);
            }

            return tblcsv;
        }
    }
}
