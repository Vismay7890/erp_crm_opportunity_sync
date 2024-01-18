using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using latlog;

namespace OpportunitySync
{
    public class AuthorizationService
    {
        public static async Task<string> GetAccessToken()
        {
            Latlog.Log(latlog.LogLevel.Info, "fetching access token..");
            string clientId = "ABHIp3ici1smzSXvw8H6Cu95RUg9YFtayYnPPIZ0ACSnMfeDz1";
            string clientSecret = "30gRrjWz6qdUFEDbbujsgCSyQhnRHes2HpavAtbf";
            string redirectUri = "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl";

            string authorizationBaseUrl = "https://appcenter.intuit.com/connect/oauth2";
            string tokenBaseUrl = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
            string username = "vismayj@commercient.online";
            string password = "#Incorrect7890";
            string authorizationUrl = $"{authorizationBaseUrl}?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=com.intuit.quickbooks.accounting&state=xyz";

            Console.WriteLine("Please authorize the application by opening the following URL in your browser:");
            Console.WriteLine(authorizationUrl);
            using (IWebDriver driver = new ChromeDriver(AppDomain.CurrentDomain.BaseDirectory))
            {
                driver.Navigate().GoToUrl(authorizationUrl);
                System.Threading.Thread.Sleep(2000);

                // Wait for the user to manually click on the link and enter the authorization code
                var usernameInput = driver.FindElement(By.XPath("/html/body/div[2]/div/div/div[1]/div/div/div[1]/div/div/div/div/div[2]/div[1]/div/form/div[1]/div[2]/div[1]/div/div/div[2]/input"));
                usernameInput.SendKeys(username);

                var signin = driver.FindElement(By.XPath("/html/body/div[2]/div/div/div[1]/div/div/div[1]/div/div/div/div/div[2]/div[1]/div/form/button"));
                signin.Click();

                // Sleep for 2 seconds to allow the page to transition
                System.Threading.Thread.Sleep(2000);

                // Enter password
                var passwordInput = driver.FindElement(By.XPath("/html/body/div[2]/div/div/div[1]/div/div/div[1]/div/div/div/div/div[2]/div[1]/div/form/div[1]/div/input"));
                passwordInput.SendKeys(password);
                var signin2 = driver.FindElement(By.XPath("/html/body/div[2]/div/div/div[1]/div/div/div[1]/div/div/div/div/div[2]/div[1]/div/form/button[2]"));
                signin2.Click();

                Console.WriteLine("Manually authorize the application in the opened browser.");
                Console.Write("Press Enter after authorization: ");
                Console.ReadLine();

            }
            Console.Write("Enter the authorization code: ");
            string authorizationCode = Console.ReadLine();

            using (HttpClient client = new HttpClient())
            {
                var tokenRequest = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", authorizationCode),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("scope", "com.intuit.quickbooks.accounting")

                };

                var tokenResponse = await client.PostAsync(tokenBaseUrl, new FormUrlEncodedContent(tokenRequest));
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenJson = JObject.Parse(tokenContent);

                return tokenJson["access_token"].ToString();
            }
        }
    }
}
