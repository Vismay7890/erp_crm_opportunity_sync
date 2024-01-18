using latlog;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace OpportunitySync
{
    internal class QuickBookOperations
    {
        private readonly string apiUrl = "https://sandbox-quickbooks.api.intuit.com/";

        public async Task CreateInvoice(string sfdata, string accessToken, string realmId, string customerRefValue, string opportunityProductsData)
        {
            string createInvoiceEndpoint = $"{apiUrl}v3/company/{realmId}/invoice";

            // Deserialize the product data from Salesforce
            var opportunityProducts = JsonConvert.DeserializeObject<dynamic>(opportunityProductsData);
            
            // Arrange the line items data in the format required for creating an invoice in QuickBooks
            var lineItems = new List<string>();

            foreach (var product in opportunityProducts.records)
            {
                Latlog.Log(LogLevel.Debug,$"ProductName:{product.ProductName},Quantity:{product.Quantity},Amount:{product.TotalPrice},QuickbookProductId:{product.QuickBooksId}");

                string lineItemData = $@"
        {{
            ""DetailType"": ""SalesItemLineDetail"",
            ""Amount"": {product.TotalPrice},
            ""SalesItemLineDetail"": {{
                ""ItemRef"": {{
                    ""value"": ""{product.QuickBooksId}"",
                    ""name"": ""{product.ProductName}""
                    
                }},
                ""UnitPrice"": ""{product.UnitPrice}"",  
                ""Qty"": ""{product.Quantity}""
            }}
        }}";

                lineItems.Add(lineItemData);
            }

            // Combine the line items data with the CustomerRef value to create the final invoice data
            string invoiceData = $@"
    {{
        ""Line"": [{string.Join(",", lineItems)}],
        ""CustomerRef"": {{
            ""name"":""rough"",
            ""value"": ""{customerRefValue}""
        }}
    }}";
            Latlog.Log(LogLevel.Debug,$"Invoice Data: {invoiceData}");
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var content = new StringContent(invoiceData, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(createInvoiceEndpoint, content);

                string responseContent = await response.Content.ReadAsStringAsync();

                // Log the QuickBooks API response content for troubleshooting
                Latlog.Log(LogLevel.Info, $"QuickBooks API Response: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    Latlog.Log(LogLevel.Info, "Invoice created successfully!");
                }
                else
                {
                    Latlog.Log(LogLevel.Error, $"Error creating invoice: {response.ReasonPhrase}");
                }
            }
        }






    }
}

