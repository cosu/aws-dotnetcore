# aws-dotnetcore

AWS related tools written in .net core.
```bash
git clone
dotnet restore
dotnet run
```

## UpdateSecurityGroup

This tool takes a hostname, resolves its ip and if needed updates an EC2 security group granting access the resolved IP. If the permission is updated then it will also
remove old  permissions from the security group. 

### Usecase 
* A host needs access to a running EC2 instance but its public IP changes frequently.

* Register the host with DynDns.

* Instead of manually managing the AWS security group permissions periodically run this tool to update the security group.   


### Configuration

```
{
  "host": "example.com",
  "securityGroupId":"sg-deadbeef",
  "port": "3389",
  "AWS": {
    "Profile": "default",
    "Region": "us-east-1"
  },
  "logLevel":"Debug"
}
```

* `host`- the DNS name of the host we're going to query
* `securityGroupId` - the security group to be updated
* `port` - the port which will be part of the permission 
* `AWS.Profile` - The AWS profile  to use to retrive credentials. The profile data is kept usually in `$HOME/.aws/credentials`. See the AWS docs for how to manage AWS credential profiles.
* `AWS.Region` - the AWS region where the security group lives
* `logLevel` - logging level. Info or Debug.

### Powershell

Altenatively there'a powershell script which performs the same operaton. See `UpdateSecurityGroup.ps1. The script has been tested with AWS Tools for PowerShell Core Edition.