# tested with AWS Tools for PowerShell Core Edition


function GetSecurityGroup($profile, $region, $securityGroupId)

{
	# get security group
	$securityGroup = Get-EC2SecurityGroup -ProfileName $profile -Region $region -GroupId $securityGroupId

	if (-not $securityGroup) 
	{
		Write-Host "Could not find SecurityGroup with id $securityGroupId"
	}
	return $securityGroup
}

function SecurityGroupContainsIpRange($securityGroup, $ipRange)
{
	# check if the ip permissions contain the current ip
	$containsCurrentIpRange=$false
	foreach ($ipPermisson in $securityGroup.IpPermissions)
	{
		If ($ipPermisson.IpRanges.Contains($ipRange))
		{
			Write-Host "Current IP range already present in SecurityGroup" $securityGroupId
			$containsCurrentIpRange=$true
			break
		}
	}
	return $containsCurrentIpRange
}
function UpdateSecurityGroup($region, $securityGroupId, $profile, $hostname, $port)
{
	# resolve host to ip
	$ipRange = [System.Net.Dns]::GetHostEntryAsync($hostname).Result.AddressList[0].IpAddressToString + "/32"

	Write-Host "Current IP address: $ipRange"

	$securitygroup = GetSecurityGroup -profile $profile -region $region -securityGroupId $securityGroupId  

	$containsCurrentIp = SecurityGroupContainsIpRange -securityGroup $securityGroup -ipRange $ipRange

	if (-not $containsCurrentIp)
	{
		Write-Host "Adding IP $ipRange to SecurityGroup $securityGroupId"

		Grant-EC2SecurityGroupIngress -ProfileName $profile -Region $region -GroupId $securityGroupId -IpPermissions `
			@{IpProtocol = 'tcp'; FromPort = $port; ToPort = $port; IpRanges =  @($ipRange)}
		
		# revoke old ips
		Write-Host "Revoking IP Ranges:" $($securityGroup.IpPermissions| select -ExpandProperty IpRanges)
		
		Revoke-EC2SecurityGroupIngress -ProfileName $profile -Region $region -GroupId $securityGroupId -IpPermissions $securityGroup.IpPermissions			
	}
}

$region = "us-east-1"
$securityGroupId = "sg-c54879ac"
$hostname = "cosu.ro"
$port = 3389
$profile = "default"

UpdateSecurityGroup  -profile $profile -region $region -securityGroupId $securityGroupId -hostname $hostname -port $port