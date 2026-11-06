[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [ValidateSet('Status','Host','Join','Install','RemoveFirewall')][string]$Mode='Status',
    [string]$HostAddress,
    [ValidateRange(1,65535)][int]$Port=7777,
    [string]$GamePath=(Join-Path (Split-Path -Parent $PSScriptRoot) 'Pinebend.exe'),
    [switch]$ConfigureFirewall,
    [switch]$Lan
)
$ErrorActionPreference='Stop'
function Get-VpnExecutable {
    $candidate=Join-Path $env:ProgramFiles 'Tailscale/tailscale.exe'
    if(Test-Path -LiteralPath $candidate){return $candidate}
    $command=Get-Command tailscale.exe -ErrorAction SilentlyContinue
    if($command){return $command.Source}
    return $null
}
function Test-Administrator {
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Test-GamePort([string]$Address,[int]$TcpPort) {
    $client=[Net.Sockets.TcpClient]::new()
    try {
        $task=$client.ConnectAsync($Address,$TcpPort)
        if(!$task.Wait(5000)){return $false}
        return $client.Connected
    } catch {return $false} finally {$client.Dispose()}
}

$ruleName="Pinebend-Playtest-TCP-$Port"
if($Mode -eq 'Install') {
    if(Get-VpnExecutable){Write-Host 'Tailscale is already installed. Open it and sign in.';return}
    if(!(Get-Command winget.exe -ErrorAction SilentlyContinue)) {
        Write-Host 'Install Tailscale from https://tailscale.com/download/windows and sign in.';return
    }
    if($PSCmdlet.ShouldProcess('Tailscale.Tailscale','Install using Windows Package Manager')) {
        & winget.exe install --id Tailscale.Tailscale --exact --source winget --accept-package-agreements --accept-source-agreements
        if($LASTEXITCODE -ne 0){throw 'Installation did not complete. Use https://tailscale.com/download/windows'}
        Write-Host 'Open Tailscale from the Start menu, sign in, then run Host setup or Join check.'
    }
    return
}
if($Mode -eq 'RemoveFirewall') {
    if(!(Test-Administrator)){throw 'Run this command as Administrator to remove the Pinebend firewall rule.'}
    if($PSCmdlet.ShouldProcess($ruleName,'Remove only this Pinebend firewall rule')) {
        Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    }
    return
}

$vpn=Get-VpnExecutable
$vpnAddress=$null
if($vpn) {
    $raw=& $vpn status --json 2>$null
    if($LASTEXITCODE -eq 0) {
        $state=($raw -join "`n") | ConvertFrom-Json
        Write-Host ('VPN state: '+$state.BackendState)
        if($state.BackendState -eq 'Running'){
            $vpnAddress=@($state.TailscaleIPs | Where-Object {$_ -match '^100\.'}) | Select-Object -First 1
            if($vpnAddress){Write-Host ('Your VPN address: '+$vpnAddress)}
        }
    } else {Write-Host 'Tailscale is installed but not ready. Open it and sign in.'}
} else {Write-Host 'Tailscale is not installed. Run Install-VPN.cmd or use https://tailscale.com/download/windows'}

if($Mode -eq 'Host') {
    if($ConfigureFirewall) {
        if(!(Test-Path -LiteralPath $GamePath -PathType Leaf)){throw 'Keep MultiplayerSetup beside Pinebend.exe, or pass -GamePath with its full path.'}
        $resolvedGame=(Resolve-Path -LiteralPath $GamePath).Path
        if(!(Test-Administrator)){throw 'Run Host-Setup.cmd as Administrator to add the scoped game firewall rule. No firewall settings were changed.'}
        $remoteRange=if($Lan){@('100.64.0.0/10','LocalSubnet')}else{@('100.64.0.0/10')}
        if($PSCmdlet.ShouldProcess("$resolvedGame TCP $Port from $($remoteRange -join ', ')",'Allow incoming Pinebend connections')) {
            Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
            New-NetFirewallRule -Name $ruleName -DisplayName "Pinebend friend playtest TCP $Port" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Program $resolvedGame -RemoteAddress $remoteRange -Profile Any | Out-Null
            Write-Host 'Pinebend firewall rule installed. Other ports and applications were not changed.'
        }
    }
    if($vpnAddress) {
        Write-Host "Give invited friends this host address: $vpnAddress   Port: $Port"
        Write-Host 'Share this PC with each friend in the Tailscale Machines page, or invite them to the same network. They must accept and sign in.'
    }
    Write-Host 'Open Pinebend > Multiplayer > Host > Open host lobby. Keep it open while friends run Join-Check.cmd.'
    Write-Host 'For LAN hosting, rerun with -Lan and use the host LAN IPv4 address.'
} elseif($Mode -eq 'Join') {
    if([string]::IsNullOrWhiteSpace($HostAddress)){$HostAddress=Read-Host 'Host VPN address (or LAN IPv4)'}
    if([Uri]::CheckHostName($HostAddress.Trim()) -eq [UriHostNameType]::Unknown){throw 'Enter a valid host IP address or hostname.'}
    $reachable=Test-GamePort $HostAddress.Trim() $Port
    if($reachable) {Write-Host "TCP $Port is reachable. Open Pinebend > Multiplayer > Join and enter $HostAddress : $Port. This checks connectivity, not gameplay."}
    else {Write-Host 'Connection failed. Verify both VPN logins, host-PC sharing/invitation acceptance, open game lobby, matching port, and the host firewall rule.'}
} else {
    Write-Host 'Status is read-only. Host-Setup.cmd configures the host; Join-Check.cmd checks a friend connection.'
}
