using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Data;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Net;

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
                return new OkObjectResult(responseMessage);
            }
            catch (Exception ex)
            {
                return new ObjectResult(ex.Message) { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }

        private async Task InsertCSVRecords(string tableName, DataTable csvRecords, List<ColumnMapping> columnMappings)
        {
            string connString = _configuration["SqlConnectionString"];

            using SqlConnection sqlConnection = new SqlConnection(connString);
            sqlConnection.Open();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connString);

            bulkCopy.DestinationTableName = tableName;
            foreach (var mapping in columnMappings)
            {
                bulkCopy.ColumnMappings.Add(mapping.Source, mapping.Destination);
            }

            await bulkCopy.WriteToServerAsync(csvRecords);
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

                    foreach (string column in line.Split(','))
                    {
                        tblcsv.Rows[tblcsv.Rows.Count - 1][colCount] = tblcsv.Columns[colCount].ColumnName == "ISN" ? rowCount.ToString() : column;                    
                        colCount++;
                    }

                    rowCount++;
                    //Console.WriteLine(line);
                }
            }
            /*
            using (TextFieldParser csvReader = new TextFieldParser(file.Content))
            {
                csvReader.SetDelimiters(new string[] { "," });
                csvReader.HasFieldsEnclosedInQuotes = true;

                while (!csvReader.EndOfData)
                {
                    var line = csvReader.ReadFields();
                    if (rowCount == 0)
                    {
                        rowCount++;
                        continue;
                    }

                    int colCount = 0;
                    tblcsv.Rows.Add();

                    foreach (string column in line)
                    {
                        tblcsv.Rows[tblcsv.Rows.Count - 1][colCount] = tblcsv.Columns[colCount].ColumnName == "ISN" ? rowCount.ToString() : column;
                        colCount++;
                    }

                    rowCount++;
                }
            }*/


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
