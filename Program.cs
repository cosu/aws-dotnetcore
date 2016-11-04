using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.EC2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;


namespace ConsoleApplication
{
    public class Program
    {
        static ILogger logger;
        
        public static void Main(string[] args = null)
        {
            var configuration = Configuration();
            
            ILoggerFactory loggerFactory = new LoggerFactory().AddConsole(LogLevel.Debug);
            
            logger = loggerFactory.CreateLogger<Program>();

            MainAsync(configuration).GetAwaiter().GetResult();

        }

        public static async Task MainAsync(IConfiguration configuration)
        {
            var ec2Client = GetEC2Client(configuration);
            string ip = await GetIpRangeFromHost(configuration["host"]);
            var securityGroup = await GetSecurityGroupWithId(ec2Client, configuration["securityGroupId"]);

            logger.LogInformation("{}({}) contains {} is {}", securityGroup.GroupName, securityGroup.GroupId, ip, 
                SecurityGroupContainsIpRange(ip, securityGroup));
        }

        static IConfiguration Configuration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("application.json");
            return builder.Build();
        }

        static IAmazonEC2 GetEC2Client(IConfiguration configuration)
        {
            var options = configuration.GetAWSOptions();
            return options.CreateServiceClient<IAmazonEC2>();
        }

        static bool SecurityGroupContainsIpRange(string ipRange, SecurityGroup securityGroup)
        {
            return  Array.Exists(securityGroup.IpPermissions.ToArray(), (IpPermission p) => {
                return p.IpRanges.Contains(ipRange);
            });
        }

        async static Task<List<SecurityGroup>> GetSecurityGroups(IAmazonEC2 ec2Client, Filter[] filters)
        {

            var describeRequest = new DescribeSecurityGroupsRequest();
            foreach (var filter in filters)
            {
                describeRequest.Filters.Add(filter);
            }

            var response = await ec2Client.DescribeSecurityGroupsAsync(describeRequest);

            List<SecurityGroup> securityGroups = response.SecurityGroups;
            
            foreach (var securityGroup in securityGroups)
            {
                LogSecurityGroup(securityGroup);                
            }
            return securityGroups;
        }

        static void LogSecurityGroup(SecurityGroup securityGroup)
        {
            logger.LogDebug("GroupId: {}, GroupName: {}, VpcId: {}",  
                securityGroup.GroupId, securityGroup.GroupName,  securityGroup.VpcId);

            foreach (var permission in securityGroup.IpPermissions)
            {
                logger.LogDebug("Protocol: {}, FromPort: {}, ToPort: {}, IpRanges: {}", 
                    permission.IpProtocol ,permission.FromPort, permission.ToPort, String.Join(",", permission.IpRanges));
            }
        }

        static async Task<SecurityGroup> GetSecurityGroupWithId(IAmazonEC2 ec2Client, string securityGroupId)
        {
            Filter vpcFilter = new Filter
            {
                Name = "group-id",
                Values = new List<string>() { securityGroupId }
            };

            var securityGroups =  await GetSecurityGroups(ec2Client, new Filter[] { vpcFilter });

            if (securityGroups.Count != 1)
                throw new Exception("Could not find securityGroup " + securityGroupId);
            return securityGroups[0];
        }

        static IpPermission CreateTCPPermission(string ipRange, int fromPort, int toPort)
        {

            var ipPermission = new IpPermission();
            ipPermission.IpProtocol = "tcp";
            ipPermission.FromPort = fromPort;
            ipPermission.ToPort = toPort;
            ipPermission.IpRanges = new List<string>() { ipRange };
            return ipPermission;
        }

        static async void Authorize(IAmazonEC2 ec2Client, string ipRange, int fromPort, int toPort, string securityGroupId)
        {
            SecurityGroup securityGroup = await GetSecurityGroupWithId(ec2Client, securityGroupId);

            var ingressRequest = new AuthorizeSecurityGroupIngressRequest();
            ingressRequest.GroupId = securityGroup.GroupId;
            IpPermission ipPermission = CreateTCPPermission(ipRange, fromPort, toPort);
            ingressRequest.IpPermissions.Add(ipPermission);
            var ingressResponse = await ec2Client.AuthorizeSecurityGroupIngressAsync(ingressRequest);
        }

        static async Task<string> GetIpRangeFromHost(string host) {
            IPHostEntry hostEntry = await Dns.GetHostEntryAsync(host);
            if (hostEntry.AddressList.Length > 0)
            {
                var ip = hostEntry.AddressList[0];
                logger.LogDebug("Found ip {}" , ip);
                // EC2 works with ip ranges so postfix it with /32
                return ip.ToString() + "/32";
            }

            throw new Exception("Could not resolve host");
        }
    }
}
