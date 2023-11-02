// See https://aka.ms/new-console-template for more information

using OpenQA.Selenium.Chrome;
using WebDriverManager.DriverConfigs.Impl;

Console.WriteLine("Building ChromeDriver");
new WebDriverManager.DriverManager().SetUpDriver(new ChromeConfig());

var options = new ChromeOptions();
options.AddArgument("--headless=new");
options.AddArgument("--disable-gpu");
options.AddArgument("--no-sandbox");
options.AddArgument("--remote-debugging-pipe");

var service = ChromeDriverService.CreateDefaultService();
//service.EnableVerboseLogging = true;
var driver = new ChromeDriver(service, options);

Console.WriteLine("Navigating to Google");
driver.Navigate().GoToUrl("https://www.google.com");
Console.WriteLine($"Title: {driver.Title}");
Console.WriteLine("Done");

