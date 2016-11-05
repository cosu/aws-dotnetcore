# tested with AWS Tools for PowerShell Core Edition

$region = "us-east-1"
$securityGroupId = "sg-c54879ac"
$hostname = "cosu.ro"

# resolve host to ip
$currentIp = [System.Net.Dns]::GetHostEntryAsync($hostname).Result.AddressList[0].IpAddressToString + "/32"

Write-Host "Current IP address: $currentIp"

# get security group
$securityGroup = Get-EC2SecurityGroup -Region $region -AccessKey $accessKey -SecretKey $secretKey -GroupId $securityGroupId

# check if the ip permissions contain the current ip
$containsCurrentIp=$false
foreach ($ipPermisson in $securityGroup.IpPermissions)
{
	If ($ipPermisson.IpRanges.Contains($currentIp))
	{
		Write-Host "Current IP already present in SecurityGroup" $securityGroupId
		$containsCurrentIp=$true		
		break
	}
}

if (-not $containsCurrentIp)
{
	Write-Host "Adding IP $currentIp to SecurityGroup $securityGroupId"
	$newPermission = @{IpProtocol = 'tcp'; FromPort = 3389; ToPort = 3389; IpRanges =  @($currentIp)}
	Grant-EC2SecurityGroupIngress -Region $region -GroupId $securityGroupId -IpPermissions $newPermission
	
	# revoke old ips
	Revoke-EC2SecurityGroupIngress -Region $region -GroupId $securityGroupId -IpPermissions $ipPermisson			
}