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
    public class UpdateSecurityGroup
    {
        static ILogger logger;
        
        public static void Main(string[] args = null)
        {
            var configuration = Configuration();

            ConfigureLogging(configuration["logLevel"]);

            MainAsync(configuration).GetAwaiter().GetResult();
        }

        public static void ConfigureLogging(string level)
        {
            LogLevel logLevel;
            LogLevel.TryParse(level, out logLevel);
            ILoggerFactory loggerFactory = new LoggerFactory().AddConsole(logLevel);
            logger = loggerFactory.CreateLogger<UpdateSecurityGroup>();            
        }

        public static async Task MainAsync(IConfiguration configuration)
        {
            var ec2Client = GetEC2Client(configuration);
            var port = int.Parse(configuration["port"]);
            var securityGroupId = configuration["securityGroupId"];
        
            var securityGroups = await GetSecurityGroupWithId(ec2Client, securityGroupId);
            var ipRange = await GetIpRangeFromHost(configuration["host"]);
            var ipPermission = CreateTCPPermission(ipRange, port, port);

            
            if (securityGroups.Count != 1) 
            {
                throw new Exception("Could not find securityGroup with id " + securityGroupId);
            }

            var securityGroup = securityGroups[0];

            if (!SecurityGroupContainsIpRange(ipRange, securityGroup))
            {
                // add ip range
                logger.LogInformation("{}({}) does not contain {}", securityGroup.GroupName, securityGroup.GroupId, ipRange);
                await Authorize(ec2Client, ipPermission, securityGroup.GroupId);

                // remove existing ip ranges    
                var oldPermissions = securityGroup.IpPermissions;    
                foreach (var oldIpPermission in oldPermissions)
                {
                    await Revoke(ec2Client, oldIpPermission, securityGroup.GroupId);
                }
            }
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
            logger.LogDebug(SecurityGroupToString(securityGroup));

            foreach (var ipPermission in securityGroup.IpPermissions)
            {
                logger.LogDebug("{} => {}" , securityGroup.GroupId,  IpPermissionToString(ipPermission));
            }
        }

        static string  SecurityGroupToString(SecurityGroup securityGroup)
        {
            return String.Format("SecurityGroup={{GroupId={0}, GroupName={1}, VpcId={2}}}", 
                securityGroup.GroupId, securityGroup.GroupName, securityGroup.VpcId);

        }

        static string IpPermissionToString(IpPermission ipPermission)
        {
            return String.Format("IpPermission={{Protocol={0}, FromPort={1}, ToPort={2}, IpRanges=[{3}]}}", 
                    ipPermission.IpProtocol ,ipPermission.FromPort, ipPermission.ToPort, String.Join(",", ipPermission.IpRanges));
        }

        static async Task<List<SecurityGroup>> GetSecurityGroupWithId(IAmazonEC2 ec2Client, string securityGroupId)
        {
            Filter groupFilter = new Filter
            {
                Name = "group-id",
                Values = new List<string>() { securityGroupId }
            };

            var securityGroups =  await GetSecurityGroups(ec2Client, new Filter[] { groupFilter });

            return securityGroups;
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

        static async Task Authorize(IAmazonEC2 ec2Client, IpPermission ipPermission, string securityGroupId)
        {
            logger.LogInformation("Authorize {}: {}", securityGroupId, IpPermissionToString(ipPermission));
            var authorizeIngressRequest = new AuthorizeSecurityGroupIngressRequest();
            authorizeIngressRequest.GroupId = securityGroupId;
            authorizeIngressRequest.IpPermissions.Add(ipPermission);
            var ingressResponse = await ec2Client.AuthorizeSecurityGroupIngressAsync(authorizeIngressRequest);
            logger.LogDebug("Authorize response code {}", ingressResponse.HttpStatusCode);
        }

        static async Task Revoke(IAmazonEC2 ec2Client, IpPermission ipPermission, string securityGroupId)
        {
            logger.LogInformation("Revoke {}: {}", securityGroupId,  IpPermissionToString(ipPermission) );
            var revokeIngressRequest = new RevokeSecurityGroupIngressRequest();
            revokeIngressRequest.GroupId = securityGroupId;
            revokeIngressRequest.IpPermissions.Add(ipPermission);
            var ingressResponse = await ec2Client.RevokeSecurityGroupIngressAsync(revokeIngressRequest);
            logger.LogDebug("Unauthorize response code " + ingressResponse.HttpStatusCode);
        }

        static async Task<string> GetIpRangeFromHost(string host) {
            IPHostEntry hostEntry = await Dns.GetHostEntryAsync(host);
            
            if (hostEntry.AddressList.Length > 0)
            {
                var ip = hostEntry.AddressList[0];
                logger.LogDebug("Resolved host {} to {}" , host, ip);
                // EC2 works with ip ranges so postfix it with /32
                return ip.ToString() + "/32";
            }
            
            throw new Exception("Could not resolve host");
        }
    }
}
