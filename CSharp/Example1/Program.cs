﻿using System;
using System.Threading.Tasks;
using QuantForce;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Example1
{
    class Program
    {
        static async Task MainAsync(string[] args)
        {
            try
            {
                // Get user name and password from environnement variables
                string userName = Environment.GetEnvironmentVariable("QFUser");
                string password = Environment.GetEnvironmentVariable("QFPassword");
                string endPoint = Environment.GetEnvironmentVariable("QFEndpoint");
                if (string.IsNullOrEmpty(endPoint))
                    endPoint = "https://portal.quanforce.net";
                string apiEndPoint = endPoint.AppendToURL("api");

                Rest client = new Rest();

                // Authentication
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                string md5password = Convert.ToBase64String(md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(password)));
                var auth = await client.PostAsync<AuthResult>(apiEndPoint.AppendToURL("auth"), new Auth()
                {
                    authType = QuantForce.Type.MD5,
                    login = userName,
                    param1 = md5password
                });

                // Search or create the project
                string projectName = "demo01";
                var projects = await client.GetAsync<ProjectList>(apiEndPoint.AppendToURL("project", auth.token));
                var project = projects.projects.Find(_ => _.name == projectName);
                if (project == null)
                {
                    Console.WriteLine("Creating a new project");
                    // Doesn't exist, create it
                    project = await client.PostAsync<Project>(apiEndPoint.AppendToURL("project", auth.token), new Project()
                    {
                        name = projectName,
                        type = 0,
                        subType = 0
                    });
                }
                if (project!=null)
                {
                    Console.WriteLine("Project");
                    Console.WriteLine(JsonConvert.SerializeObject(project));
                }

                // Upload the dataset
                string data = "Telco_customer_churn_v1.csv";
                var task = await client.PostRawAsync<AsyncTaskStatus>(apiEndPoint.AppendToURL("dataset", "csv", auth.token, project.id, "raw"), System.IO.File.ReadAllBytes(data));
                if (task != null)
                {
                    // Wait for the dataset to be integrated
                    while ((int)task.status < 400)
                    {
                        Console.WriteLine("Task status = {0}", task.status);
                        await Task.Delay(1000);
                        task = await client.GetAsync<AsyncTaskStatus>(apiEndPoint.AppendToURL("task", auth.token, project.id, task.id));
                    }
                    Console.WriteLine("Task status = {0}", task.status);

                    // Get dataset infos
                    var dataset = await client.GetAsync<Dataset>(apiEndPoint.AppendToURL("dataset", auth.token, project.id));
                    // Force the target
                    foreach (Column column in dataset.columns)
                    {
                        if (column.columType == VariableType.Target)
                            column.columType = VariableType.Ignore;
                        if (column.name == "Churn_Value")
                            column.columType = VariableType.Target;
                        Console.WriteLine($"Column {column.name} is {column.columType}.");
                    }
                    // Update column qualifications
                    await client.PostAsync<Dataset>(apiEndPoint.AppendToURL("dataset", auth.token, project.id), dataset);

                    // Do the binning

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        static void Main(string[] args)
        {
            Task.WaitAll(Task.Run(async () => await MainAsync(args)));
            Console.ReadLine();
        }
    }
}