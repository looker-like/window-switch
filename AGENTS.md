# Agent Workflow

## Git 操作流程

1. 开始前先检查仓库状态：
   - `git status --short --branch`
   - `git log --oneline --decorate -5`

2. 提交前先确认 `.gitignore` 覆盖本地和构建产物，至少不提交：
   - `bin/`
   - `obj/`
   - `.vs/`
   - `TestResults/`
   - 发布输出和临时文件

3. 非平凡改动先从 `main` 切功能分支，分支名前缀使用 `codex/`：
   - `git switch main`
   - `git switch -c codex/<short-task-name>`

4. 保持小步提交。每个提交只覆盖一个清晰意图，例如：
   - 工作流或 hook
   - UI 行为调整
   - 快捷键功能
   - 测试或验证补充

   任何形式的修改完成后都要及时做细颗粒度提交；不要把多个不相关意图混在同一个提交里。

5. 提交前和提交后都使用本仓库 hook，并把单个代码文件长度规则作为固定门禁。仓库已配置：
   - 提交前 hook：`.githooks/pre-commit`
   - 提交后 hook：`.githooks/post-commit`
   - 本机配置：`git config core.hooksPath .githooks`

   pre-commit 当前会执行：
   - `dotnet format WindowSwitch.sln --verify-no-changes --no-restore`
   - `dotnet test WindowSwitch.sln --no-restore`

   post-commit 仅在本次提交包含 `.cs` 文件变更时执行：
   - `python scripts/check_code_file_length.py --max-lines 300`

   每次提交都必须遵循细颗粒度提交原则。先提交当前清晰意图；提交完成后，如果本次提交包含 `.cs` 文件变更，post-commit 会逐个扫描 `.cs` 代码文件，检查是否有任一代码文件超过 300 行。纯文档、配置、资源等不涉及 C# 代码的提交不触发该检查。这里不是限制单个函数/方法 300 行。如果存在单个代码文件超过 300 行，立刻进行重构拆分该文件，并用第二次细颗粒度提交记录该重构。

6. 如果提交被运行中的 `WindowSwitch.exe` 锁住，先确认进程路径属于当前仓库，再结束进程后重试：
   - `Get-Process WindowSwitch -ErrorAction SilentlyContinue | Select-Object Id,Path`
   - `Stop-Process -Id <pid> -Force`

7. 功能完成后在功能分支上跑最终验证：
   - `dotnet build WindowSwitch.sln -c Release`
   - `dotnet test WindowSwitch.sln -c Release --no-build`
   - `dotnet publish WindowSwitch/WindowSwitch.csproj -c Release -r win-x64 --self-contained false`

8. 合回 `main` 时优先使用快进合并：
   - `git switch main`
   - `git merge --ff-only codex/<short-task-name>`

9. 合并后再次确认：
   - `git status --short --branch`
   - `git log --oneline --decorate -6`

10. 不使用破坏性命令清理用户改动。除非用户明确要求，不运行：
    - `git reset --hard`
    - `git checkout -- <path>`
    - 任何会丢弃未提交改动的命令
