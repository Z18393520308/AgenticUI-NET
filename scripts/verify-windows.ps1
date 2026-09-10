#Requires -Version 5.1
<#
Windows 联合验收入口。只构建/运行测试，不运行模型、不连接生产设备、不提交或发布。
可选 AgentRepositoryPath 指向另一仓库；目录位置自由，不把本机路径写进项目。
#>
[CmdletBinding()]
param([string]$AgentRepositoryPath)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw '此入口需要 Windows。macOS 编译不能替代 Windows Desktop 验收。' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '请先安装 .NET 8 SDK。' }
$runtimes = & dotnet --list-runtimes
if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.WindowsDesktop.App 8\.')) {
    throw '请安装 .NET 8 Windows Desktop Runtime（WPF / WinForms）。'
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repositoryRoot ('artifacts/windows-acceptance/' + $runName)
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$results = New-Object 'System.Collections.Generic.List[string]'
$failure = $null

function Invoke-DotNetCheck {
    param([string]$Label, [string]$WorkingDirectory, [string[]]$DotNetArguments)
    Write-Host ('检查：' + $Label) -ForegroundColor Cyan
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & dotnet @DotNetArguments
        if ($LASTEXITCODE -ne 0) { throw ($Label + ' 失败，退出码 ' + $LASTEXITCODE) }
        $results.Add('- [x] ' + $Label)
    }
    catch { $results.Add('- [ ] ' + $Label + '：失败'); throw }
    finally { Pop-Location }
}

try {
    Invoke-DotNetCheck '控件完整方案还原' $repositoryRoot @('restore', 'AgenticUI.NET.sln')
    Invoke-DotNetCheck '控件 Release 构建（含 net48）' $repositoryRoot @('build', 'AgenticUI.NET.sln', '-c', 'Release', '--no-restore')
    Invoke-DotNetCheck '控件完整测试（含真实 WPF / WinForms 程序集）' $repositoryRoot @(
        'test', 'AgenticUI.NET.sln', '-c', 'Release', '--no-build', '--no-restore',
        '--logger', 'trx', '--results-directory', (Join-Path $reportDirectory 'controls'))

    if ($AgentRepositoryPath) {
        $agentRoot = (Resolve-Path -LiteralPath $AgentRepositoryPath).Path
        $agentScript = Join-Path $agentRoot 'scripts/verify.ps1'
        if (-not (Test-Path -LiteralPath $agentScript)) { throw 'Agent 仓库中缺少 scripts/verify.ps1。请同步第二阶段修改。' }
        # 对本地包做校验，防止只复制新版控制端代码却仍使用旧控件二进制。
        $manifestPath = Join-Path $agentRoot 'packages/guidance-development.json'
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw '缺少第三阶段联调包清单。请同时同步 packages。' }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        [xml]$props = Get-Content -LiteralPath (Join-Path $agentRoot 'Directory.Build.props') -Raw
        if ($props.Project.PropertyGroup.AgenticUIVersion -ne $manifest.version) { throw 'AgenticUIVersion 与联调包清单不一致。' }
        foreach ($package in $manifest.packages) {
            if ($package.file -ne [IO.Path]::GetFileName($package.file)) { throw '清单中的包名不能包含目录。' }
            $packagePath = Join-Path (Join-Path $agentRoot 'packages') $package.file
            if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ne $package.sha256) { throw ('联调包校验失败：' + $package.file) }
        }
        $results.Add('- [x] Agent 联调包版本与 SHA-256 校验')
        Push-Location -LiteralPath $agentRoot
        try {
            # 复用第二阶段的 7 项验证：核心、可移植/Windows 编排、离线评测及许可文件。
            & $agentScript -RestorePackagesPath (Join-Path $agentRoot ('artifacts/windows-nuget/' + $runName)) -ResultsDirectory (Join-Path $reportDirectory 'agent')
            $results.Add('- [x] Agent scripts/verify.ps1 全部检查')
        }
        catch { $results.Add('- [ ] Agent scripts/verify.ps1：失败'); throw }
        finally { Pop-Location }
    }
    else { $results.Add('- [ ] Agent 未验收：未指定 AgentRepositoryPath') }
}
catch { $failure = $_; Write-Warning '自动检查未全部通过，请保留终端错误与 TRX 文件。' }
finally {
    $report = @('# Windows 联合验收记录', '', ('日期：' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')), '',
        '自动测试不等于人工视觉验收；此入口不启动业务软件、不调用模型。', '') + $results.ToArray() + @('',
        '## 仍需人工验收', '', '- [ ] .NET 8 和 .NET Framework 4.8 实机宿主分别运行',
        '- [ ] 描边、编号、动态气泡、焦点与点击穿透', '- [ ] 只读输入（包含敏感输入）、绑定校验和模态弹窗',
        '- [ ] 树路径、展开/折叠事件、同名节点拒绝', '- [ ] 多连接、取消、断线与重连清理',
        '- [ ] 表格排序/滚动、高 DPI、多显示器', '- [ ] 测试数据库上的三模式与技能回放',
        '- [ ] 隔离环境的真实模型联调（需要单独配置，勿将密钥写入报告）')
    if ($failure) { $report += @('', '结果：自动检查失败。详见终端错误。') }
    else { $report += @('', '结果：所选自动检查通过；上述人工项目仍未验收。') }
    $reportFile = Join-Path $reportDirectory 'report.md'
    $report | Set-Content -LiteralPath $reportFile -Encoding UTF8
    Write-Host ('验收记录：' + $reportFile)
}
if ($failure) { throw $failure }
