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

## TDD 方法论规范

> 所有重构与新功能开发，必须严格遵循 TDD（测试驱动开发）循环。

### 核心循环：Red → Green → Refactor

每一个最小可交付单元都必须经历完整的三步循环，不可跳过任何阶段：

1. **Red（红灯）** — 先写一个**失败的测试**
   - 测试必须描述**期望的行为**，而非当前实现
   - 运行 `dotnet test` 确认测试确实**失败**（不是编译错误，是断言失败）
   - 如果测试因编译错误而失败，需先写最少的 stub 让其能编译并运行到失败断言
   - 禁止在没有失败测试的情况下直接修改生产代码

2. **Green（绿灯）** — 写**最少的生产代码**让测试通过
   - 只写恰好能让测试通过的代码，不多一行
   - 不允许在此阶段进行架构优化或"顺手"重构
   - 运行 `dotnet test` 确认**所有测试通过**（绿灯）

3. **Refactor（重构）** — 在绿灯状态下改善代码结构
   - 消除重复、改善命名、提取方法/类
   - 每次小重构后立即运行 `dotnet test` 确认仍是绿灯
   - 重构不改变可观测行为，只改善内部结构

### 提交策略与循环对应

- **Red 阶段提交**：提交信息前缀 `test:` — 仅包含失败测试（允许临时 stub）
  - 示例：`test: HotkeySequence 忽略超时后的按键`
- **Green 阶段提交**：提交信息前缀 `feat:` 或 `fix:` — 生产代码让测试变绿
  - 示例：`feat: 实现 HotkeySequence 超时重置逻辑`
- **Refactor 阶段提交**：提交信息前缀 `refactor:` — 结构改善，不改变行为
  - 示例：`refactor: 提取 ResetSequenceState 方法`

每个 TDD 循环对应 2–3 次细粒度 Git 提交（Red + Green + 可选 Refactor）。

### 测试命名规范

测试方法名必须体现**被测场景**，格式为：

```
[被测方法或行为]_[触发条件]_[期望结果]
```

示例：
- `ProcessKey_WhenTimeoutExpired_ResetsSequence`
- `SwitchDesktop_GivenInvalidIndex_DoesNotThrow`
- `ViewModel_WhenSettingsLoaded_PopulatesHotkeyList`

### 测试组织原则

- 每个测试文件对应一个被测类（`FooTests.cs` 测 `Foo.cs`）
- 使用 `Arrange / Act / Assert` 注释块区分三个阶段（复杂用例）
- 一个测试只验证**一个行为**；禁止在单个 `[Fact]` 中混合多个断言意图
- Fake/Mock 放在独立文件（如现有的 `FakeVirtualDesktopService.cs`），不内嵌在测试方法中
- 测试项目不引用 UI 层（WPF/XAML）；如需测试 ViewModel，通过接口或内存实现隔离

### 重构时的 TDD 约束

- **不允许在没有覆盖测试的情况下重构生产代码**
  - 若目标代码缺少测试覆盖，必须先补充测试（Red→Green），再重构
- **重构步骤必须保持测试始终绿灯**
  - 每次提取方法、移动类、改名后立即运行 `dotnet test`
  - 如果重构导致测试红灯，立即回滚该步骤
- **禁止"一次性大重构"**
  - 每次重构只做一件事（单一职责原则在重构步骤上同样适用）

### 每日 TDD 工作节奏

```
开始任务 → [Red] 写失败测试 → 提交 test:
         → [Green] 最小实现 → 提交 feat:/fix:
         → [Refactor] 改善结构 → 提交 refactor:（可选）
         → 下一个行为 → 重复循环
```

任何时刻，运行 `dotnet test` 应该是你的第一反应，而不是最后一步。
