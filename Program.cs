using System;
using System.Threading.Tasks;
using System.Configuration;
using latlog;
using Newtonsoft.Json;
using System.Text.Json.Nodes;
using System.Text;
using Newtonsoft.Json.Linq;
using OpportunitySync;
namespace OpportunitySync
{
    internal class Program
    {
        private static SalesforceClient CreateClient()
        {
            Latlog.Log(LogLevel.Info, "Creating Salesforce client...");
            return new SalesforceClient
            {
                Username = ConfigurationManager.AppSettings["username"],
                Password = ConfigurationManager.AppSettings["password"],
                Token = ConfigurationManager.AppSettings["token"],
                ClientId = ConfigurationManager.AppSettings["clientId"],
                ClientSecret = ConfigurationManager.AppSettings["clientSecret"]
            };
        }
        static async Task Main()
        {
            Latlog.Log(LogLevel.Info, "Program Started...");

            string accessToken = await AuthorizationService.GetAccessToken();
            string realmId = "4620816365383903640";
            var client = CreateClient();
            client.Login();

            if (!string.IsNullOrEmpty(accessToken))
            {
                var customerService = new QuickBookOperations();
                while (true)
                {
                    Console.WriteLine("Select an option:");
                    Console.WriteLine("1. Fetch Opportunity and create Invoice if Closed Won");
                    Console.WriteLine("2. Potato/Patato..");
                    
                    Console.WriteLine("0. Exit ");

                    Console.Write("Enter your choice (1-0): ");
                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                    
                        case "1":
                            string sfdata = client.Query();
                            string CustomerRef = client.GetCustomerRefValue(sfdata);
                            string Productdetails = client.GetOpportunityProducts(sfdata , accessToken , realmId);
                           await customerService.CreateInvoice(sfdata , accessToken , realmId,CustomerRef , Productdetails);
                            break;
                        case "2":
                            break;
                        case "0":
                            Console.WriteLine("Exiting...");
                            Environment.Exit(0);
                            break;
                        default:
                            Latlog.Log(LogLevel.Info, "Invalid choice...");

                            break;
                    }
                }
            }
            else
            {
                Latlog.Log(LogLevel.Debug, "Access token not found");
            }
        }
    }
}
