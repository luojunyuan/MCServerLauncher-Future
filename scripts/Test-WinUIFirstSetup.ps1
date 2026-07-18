param(
    [string] $PublishDirectory = (Join-Path $PSScriptRoot '..\src\MCServerLauncher.WinUI\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish'),
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
$PublishDirectory = [IO.Path]::GetFullPath($PublishDirectory)
$executable = Join-Path $PublishDirectory 'MCServerLauncher.WinUI.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published WinUI executable was not found: $executable"
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testDirectory = [IO.Path]::GetFullPath((Join-Path $tempRoot "mcsl-winui-first-setup-$([Guid]::NewGuid().ToString('N'))"))
if (-not $testDirectory.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe first-setup test directory.'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$process = $null

function New-ProcessCondition {
    [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $process.Id)
}

function Find-Element {
    param(
        [string] $AutomationId,
        [string] $Name,
        [System.Windows.Automation.ControlType] $ControlType,
        [switch] $Enabled
    )

    $conditions = [System.Collections.Generic.List[System.Windows.Automation.Condition]]::new()
    $conditions.Add((New-ProcessCondition))
    if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
        $conditions.Add([System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            $AutomationId))
    }
    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        $conditions.Add([System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name))
    }
    if ($null -ne $ControlType) {
        $conditions.Add([System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            $ControlType))
    }
    if ($Enabled) {
        $conditions.Add([System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::IsEnabledProperty,
            $true))
    }

    $condition = [System.Windows.Automation.AndCondition]::new($conditions.ToArray())
    [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
}

function Wait-Element {
    param(
        [string] $AutomationId,
        [string] $Name,
        [System.Windows.Automation.ControlType] $ControlType,
        [switch] $Enabled,
        [int] $TimeoutSeconds = 8
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $findParameters = @{
        AutomationId = $AutomationId
        Name = $Name
        ControlType = $ControlType
        Enabled = $Enabled
    }
    do {
        $element = Find-Element @findParameters
        if ($null -ne $element) {
            return $element
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    $description = if ($AutomationId) { "AutomationId '$AutomationId'" } else { "Name '$Name'" }
    throw "Timed out waiting for $description."
}

function Invoke-Element {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement] $Element
    )

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    Start-Sleep -Milliseconds 500
}

function Select-Element {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement] $Element
    )

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
    Start-Sleep -Milliseconds 600
}

function Select-ComboItem {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement] $ComboBox,
        [Parameter(Mandatory)]
        [string] $Name
    )

    $expand = $ComboBox.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    Select-Element (Wait-Element -Name $Name -ControlType ([System.Windows.Automation.ControlType]::ListItem))
}

function Wait-ElementName {
    param(
        [Parameter(Mandatory)]
        [string] $AutomationId,
        [Parameter(Mandatory)]
        [string] $Expected,
        [int] $TimeoutSeconds = 5
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = Find-Element -AutomationId $AutomationId
        if ($null -ne $element -and $element.Current.Name -eq $Expected) {
            return $element
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for AutomationId '$AutomationId' to have name '$Expected'."
}

function Wait-Selected {
    param(
        [Parameter(Mandatory)]
        [string] $AutomationId,
        [int] $TimeoutSeconds = 5
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = Find-Element -AutomationId $AutomationId
        if ($null -ne $element) {
            $selected = $element.GetCurrentPropertyValue([System.Windows.Automation.SelectionItemPattern]::IsSelectedProperty)
            if ($selected) {
                return $element
            }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for AutomationId '$AutomationId' to be selected."
}

function Open-NavigationPane {
    if ($null -ne (Find-Element -AutomationId 'HomeNavigationItem')) {
        return
    }

    Invoke-Element (Wait-Element `
        -AutomationId 'TogglePaneButton' `
        -ControlType ([System.Windows.Automation.ControlType]::Button) `
        -Enabled)
    [void](Wait-Element `
        -AutomationId 'HomeNavigationItem' `
        -ControlType ([System.Windows.Automation.ControlType]::ListItem) `
        -Enabled)
}

function Wait-NavigationItem {
    param(
        [Parameter(Mandatory)]
        [string] $AutomationId
    )

    Open-NavigationPane
    Wait-Element `
        -AutomationId $AutomationId `
        -ControlType ([System.Windows.Automation.ControlType]::ListItem) `
        -Enabled
}

function Wait-NavigationSelected {
    param(
        [Parameter(Mandatory)]
        [string] $AutomationId
    )

    Open-NavigationPane
    Wait-Selected -AutomationId $AutomationId
}

function Wait-SettingValue {
    param(
        [Parameter(Mandatory)]
        [string] $Path,
        [Parameter(Mandatory)]
        [string] $Property,
        [Parameter(Mandatory)]
        $Expected,
        [int] $TimeoutSeconds = 5
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $Path) {
            $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
            $actual = $document.App.$Property
            if ($actual -eq $Expected) {
                return $document
            }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for App.$Property to equal '$Expected'."
}

function Get-SelectedName {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement] $Element
    )

    $selection = $Element.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
    $selected = $selection.Current.GetSelection()
    if ($selected.Length -eq 0) {
        return [string]::Empty
    }
    $selected[0].Current.Name
}

function Assert-Equal {
    param($Actual, $Expected, [string] $Message)
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

try {
    New-Item -ItemType Directory -Path $testDirectory | Out-Null
    Copy-Item -Path (Join-Path $PublishDirectory '*') -Destination $testDirectory -Recurse -Force
    $testDataDirectory = Join-Path $testDirectory 'Data'
    if (Test-Path -LiteralPath $testDataDirectory) {
        Remove-Item -LiteralPath $testDataDirectory -Recurse -Force
    }

    $process = Start-Process `
        -FilePath (Join-Path $testDirectory 'MCServerLauncher.WinUI.exe') `
        -WorkingDirectory $testDirectory `
        -WindowStyle Minimized `
        -PassThru

    for ($attempt = 0; $attempt -lt 30 -and $process.MainWindowHandle -eq 0; $attempt++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    if ($process.MainWindowHandle -eq 0) {
        throw 'CoreIsland main window was not created.'
    }
    Start-Sleep -Seconds 3

    $language = Wait-Element -AutomationId 'LanguageComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
    $expand = $language.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $english = Wait-Element -Name 'English (US)' -ControlType ([System.Windows.Automation.ControlType]::ListItem)
    $english.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 800

    $next = Wait-Element -AutomationId 'ContinueButton' -ControlType ([System.Windows.Automation.ControlType]::Button)
    Assert-Equal $next.Current.Name 'Next' 'Runtime language switch did not refresh the first-setup button.'
    Invoke-Element $next

    $eulaUrl = Wait-Element -AutomationId 'EulaUrlTextBlock' -ControlType ([System.Windows.Automation.ControlType]::Text)
    Assert-Equal $eulaUrl.Current.Name 'https://future.mcsl.com.cn/en/eula.html' 'The localized EULA URL is incorrect.'
    $openEula = Wait-Element -AutomationId 'OpenEulaButton' -ControlType ([System.Windows.Automation.ControlType]::Button)
    if (-not $openEula.Current.IsEnabled) {
        throw 'The external EULA button is disabled.'
    }

    $accept = Wait-Element -AutomationId 'AcceptButton' -ControlType ([System.Windows.Automation.ControlType]::Button)
    if ($accept.Current.IsEnabled) {
        throw 'The EULA accept button is enabled before the countdown.'
    }
    if ($accept.Current.Name -notmatch '^Agree \(1[0-5]s\)$') {
        throw "Unexpected EULA countdown text: '$($accept.Current.Name)'."
    }

    Start-Sleep -Seconds 16
    $accept = Wait-Element -AutomationId 'AcceptButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled
    Assert-Equal $accept.Current.Name 'Agree' 'The EULA accept button did not finish its countdown.'
    Invoke-Element $accept
    Invoke-Element (Wait-Element -Name 'Agree' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)

    [void](Wait-Element -AutomationId 'DaemonNavigationItem' -ControlType ([System.Windows.Automation.ControlType]::ListItem) -Enabled)
    Invoke-Element (Wait-Element -AutomationId 'UseRemoteDaemonButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)
    Invoke-Element (Wait-Element -AutomationId 'DaemonActionButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)

    $scheme = Wait-Element -AutomationId 'DaemonSchemeComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
    Assert-Equal (Get-SelectedName $scheme) 'ws://' 'The daemon dialog did not default to ws://.'
    [void](Wait-Element -AutomationId 'DaemonEndpointTextBox' -ControlType ([System.Windows.Automation.ControlType]::Edit))
    $port = Wait-Element -AutomationId 'DaemonPortTextBox' -ControlType ([System.Windows.Automation.ControlType]::Edit)
    [void](Wait-Element -AutomationId 'DaemonTokenBox' -ControlType ([System.Windows.Automation.ControlType]::Edit))
    Assert-Equal $port.GetCurrentPropertyValue([System.Windows.Automation.ValuePattern]::ValueProperty) '25565' 'The daemon port default is incorrect.'
    Invoke-Element (Wait-Element -Name 'Back' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)

    Invoke-Element (Wait-Element -AutomationId 'DaemonSkipButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)
    Invoke-Element (Wait-Element -Name 'Skip temporarily' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)
    [void](Wait-Element -AutomationId 'WelcomeNavigationItem' -ControlType ([System.Windows.Automation.ControlType]::ListItem) -Enabled)
    Invoke-Element (Wait-Element -AutomationId 'ContinueButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)

    [void](Wait-NavigationItem -AutomationId 'HomeNavigationItem')
    [void](Wait-NavigationItem -AutomationId 'SettingsNavigationItem')

    $settingsPath = Join-Path $testDirectory 'Data\Configuration\MCSL\Settings.json'
    $settings = Wait-SettingValue -Path $settingsPath -Property 'IsFirstSetupFinished' -Expected $true
    Assert-Equal $settings.App.Language 'en-US' 'The selected language was not persisted.'
    Assert-Equal $settings.App.IsAppEulaAccepted $true 'EULA acceptance was not persisted.'

    Select-Element (Wait-NavigationItem -AutomationId 'CreateNavigationItem')
    Invoke-Element (Wait-Element -AutomationId 'CreateInstanceConnectDaemonButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)
    [void](Wait-NavigationSelected -AutomationId 'DaemonManagerNavigationItem')
    [void](Wait-Element -AutomationId 'DaemonSchemeComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox))
    Invoke-Element (Wait-Element -Name 'Cancel' -ControlType ([System.Windows.Automation.ControlType]::Button) -Enabled)

    Select-Element (Wait-NavigationItem -AutomationId 'SettingsNavigationItem')
    [void](Wait-NavigationSelected -AutomationId 'SettingsNavigationItem')
    [void](Wait-Element -AutomationId 'ThemeComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox))

    $languageCases = @(
        @{ Name = '日本語'; Code = 'ja-JP'; Settings = 'プログラム設定' },
        @{ Name = 'Русский'; Code = 'ru-RU'; Settings = 'Настройки программы' },
        @{ Name = '简体中文 (中国)'; Code = 'zh-CN'; Settings = '程序设置' },
        @{ Name = '繁體中文 (中国香港)'; Code = 'zh-HK'; Settings = 'Settings' },
        @{ Name = '正體中文 (中国台湾)'; Code = 'zh-TW'; Settings = '程式設定' },
        @{ Name = 'English (US)'; Code = 'en-US'; Settings = 'Settings' }
    )
    foreach ($case in $languageCases) {
        $languageCombo = Wait-Element -AutomationId 'LanguageComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
        Select-ComboItem -ComboBox $languageCombo -Name $case.Name
        [void](Wait-ElementName -AutomationId 'SettingsNavigationItem' -Expected $case.Settings)
        [void](Wait-SettingValue -Path $settingsPath -Property 'Language' -Expected $case.Code)
    }

    $themeCombo = Wait-Element -AutomationId 'ThemeComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
    Select-ComboItem -ComboBox $themeCombo -Name 'Light'
    [void](Wait-SettingValue -Path $settingsPath -Property 'Theme' -Expected 'light')
    $themeCombo = Wait-Element -AutomationId 'ThemeComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
    Select-ComboItem -ComboBox $themeCombo -Name 'Dark'
    [void](Wait-SettingValue -Path $settingsPath -Property 'Theme' -Expected 'dark')
    $themeCombo = Wait-Element -AutomationId 'ThemeComboBox' -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
    Select-ComboItem -ComboBox $themeCombo -Name 'System'
    [void](Wait-SettingValue -Path $settingsPath -Property 'Theme' -Expected 'auto')

    foreach ($navigationId in @(
        'HomeNavigationItem',
        'CreateNavigationItem',
        'InstanceManagerNavigationItem',
        'DaemonManagerNavigationItem',
        'ResourceDownloadNavigationItem',
        'HelpNavigationItem',
        'SettingsNavigationItem')) {
        Select-Element (Wait-NavigationItem -AutomationId $navigationId)
        [void](Wait-NavigationSelected -AutomationId $navigationId)
    }

    $logs = Get-ChildItem -LiteralPath (Join-Path $testDirectory 'Data\Logs\WinUI') -Filter 'WinUILog-*.txt' -File -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    $settingsCreations = @($logs | Select-String -SimpleMatch '[WinUI] Settings page created')
    Assert-Equal $settingsCreations.Count 1 'The settings page was recreated during shell navigation.'
    $errors = @($logs | Select-String -Pattern '\b(ERR|FTL)\]')
    if ($errors.Count -gt 0) {
        throw "The first-setup smoke test produced error logs: $($errors.Line -join ' | ')"
    }

    Write-Host 'Verified first setup, daemon action navigation, six runtime languages, themes, shell navigation, page caching, and persistence.'
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
    }

    if ($KeepArtifacts) {
        Write-Host "First-setup test artifacts: $testDirectory"
    }
    elseif (Test-Path -LiteralPath $testDirectory) {
        $removed = $false
        for ($attempt = 0; $attempt -lt 20 -and -not $removed; $attempt++) {
            try {
                Remove-Item -LiteralPath $testDirectory -Recurse -Force
                $removed = $true
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }
        if (-not $removed) {
            Write-Warning "Could not remove first-setup test directory: $testDirectory"
        }
    }
}
