# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

* Codex（GPT-5）

---

## 通用四問

### 1. 我的任務拆解

我一開始先要求 agent：

> `read thru all the .md, and go thru the project and show me the place that can be enhanced`

之後我把工作改成「一次只做一件、每件先讓我 review，再 commit / push」：

> `all you listed, but one by one. btw did you go thru the activity-guideline.md traning 1-4 ?`

實際執行順序如下：

1. 先讀 repository 內的 Markdown、`activity-guideline.md` 與現有程式，建立 Web / Core / Infrastructure 的分層理解。
2. 練習 1：加入 Codex 專案設定、規則、hooks、agent 與 `fix-bug` skill。
3. 練習 2：三個 bug 各自走「先寫會失敗的回歸測試 → 修正 → focused test → full test → review → 獨立 commit」。
4. 練習 3：先看計畫，再實作低庫存頁；我對 `LowStockProduct` 的位置與整體結構有疑問，所以沒有直接接受第一次版本，而是多次 review，重新檢查 repository ownership、dependency direction 與命名後才採用最後的調整。
5. 練習 4：先看重構計畫，再比較不同 helper 設計的責任與耦合程度，最後只改 `OrderService.cs`。
6. 每個階段都由我明確說 `approve` 後才 commit / push，commit body 固定加入 Summary、Behavior 或 Verification。

原本預期一路照初版計畫做完，但練習 3 的順序有改。第一次低庫存查詢在 EF Core InMemory 測試是綠的，實際用 SQL Server 跑 `/Products/LowStock` 時卻出現：

```text
System.InvalidOperationException: The LINQ expression ... could not be translated.
```

所以工作多了「實際 SQL route smoke test → 修正 query translation → 再測一次」。

最後採用較清楚的責任分工：`ProductRepository` 查符合門檻的商品、`OrderRepository` 統計這些商品近 30 天的銷量、`ProductService` 合併成 `LowStockProductSummary`。

### 2. AI 幫上大忙的地方

最有幫助的是把跨層功能拆開檢查，並且真的跑到 SQL Server，而不是只停在 InMemory test。

### 3. AI 誤導我的地方，與我如何發現

練習 3 的第一版是最明顯的例子。

一開始 agent 把 `LowStockProduct` 放在 `OrderHub.Core.Services`，而且讓 `ProductRepository` 直接查 `OrderItems`。雖然程式可以解釋成「低庫存報表以 Product 為主」，但我看 diff 時仍覺得結構不自然，所以我直接問：

> `why we have record LowStockProduct, inside our OrderHub.Core.Services ? why it was different then others ?`

過程中 agent 曾建議移到 `Core.Models`，我又發現專案現有的 `NewOrderLine` record 就放在 Services，因此把它移回來。這也讓我知道「檔案放得像現有範例」仍不代表責任已經切好；真正的問題是 repository contract 不應為了這個 report 互相混用資料責任。

最後我要求重新檢查 dependency direction、repository ownership、查詢範圍與命名，才收斂成現在的結構：

```text
ProductsController
  → ProductService
      → ProductRepository：篩選低庫存商品
      → OrderRepository：依 product IDs 統計銷量
  → LowStockProductSummary
```

另一個誤判是 InMemory tests 全綠後，agent 一度認為 EF query 沒問題。實際跑 SQL Server route 才抓到 `could not be translated`。原因是 EF SQL provider 無法翻譯 projection 後的排序方式；InMemory provider 沒有暴露這個問題。這次是靠真實 provider 的 smoke test 抓到，不是靠單元測試。

HTTP 驗證也出現過一次誤報。腳本直接比對中文字串時得到 `INVALID_VALIDATION=False`，但檢查 raw HTML 後看到：

```html
<span class="text-danger field-validation-error" ...>
    &#x5EAB;&#x5B58;&#x9580;&#x6ABB;...
</span>
```

Razor 把中文編成 HTML numeric entities，功能其實正常。後來驗證方式改成檢查 `field-validation-error`、HTTP status 與表格是否隱藏。

### 4. 我會帶回日常工作的一招

我會固定使用以下流程處理 agent 產出的變更：

1. 先請 agent 只讀規格與現有程式，列出要改的檔案、每層責任與測試邊界。
2. 我先 review 計畫，特別檢查是否多做了不在需求內的重構。
3. Bug 先建立一個會失敗的 regression test，記錄修正前的實際值。
4. 實作後依序跑 focused tests、full tests、Release build、format check。
5. 有 EF query、routing、binding 或 validation 時，再用真實 SQL provider / HTTP route 做 smoke test，不能只相信 InMemory。
6. 看完整 diff，對命名或責任有疑問就先停下來問；必要時列出不同結構方案，比較 dependency direction、資料責任、耦合程度與未來擴充成本，再採用適合目前專案的做法。
7. 我明確 approve 後才 commit / push；commit 使用固定格式：

```text
<type>(<scope>): <summary>

Summary:
- ...

Behavior:
- ...

Verification:
- ...
```

這套流程的重點不是「多跑一次測試」，而是每種風險用對應的驗證方式：商業規則用 regression test、EF translation 用 SQL Server、MVC validation 用 HTTP response、架構問題用 diff 與責任比較。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

#### 練習 1

* [x] 我能不看筆記說出三個專案的職責：

  * `OrderHub.Web`：Controller、ViewModel、Razor View 與 UI mapping。
  * `OrderHub.Core`：domain、service、商業規則與 repository interface。
  * `OrderHub.Infrastructure`：EF Core DbContext、repository implementation、migration 與 seed data。
* [x] 我核對過建單流程，也找到一個容易被過度簡化的地方：建單不是「全部商品先驗證完才扣庫存」。目前程式會逐項查商品，通過就先扣 tracked entity 的庫存並建立 item；如果後面的商品失敗，訂單不會 `SaveChanges`，但同一個 DbContext 內前面商品的 tracked stock 已經被修改。
* [x] 我知道商業邏輯應放在 Core service；新增 MVC 頁面通常會動 Controller、Service、Repository、ViewModel、View 與 Tests，必要時再調整導覽列與 DI。
* [x] Codex 專案設定已獨立 commit：`9bceca4 chore: configure Codex project workflow`。

#### 練習 2

* [~] 我沒有把三個 bug 都先親自在瀏覽器逐項重現；本次主要由 failing regression tests 重現。下次應先記錄頁面上的 page、金額與庫存數字，再交給 agent。
* [x] 我有使用具體數字驗證根因：

  * pagination page 1 不應跳過第一頁資料；
  * Gold `1000` 應為 `900`，不可變成 `810`；
  * stock `10 → 7 → cancel` 應回到 `10`，修正前是 `7`。
* [~] 三個症狀都有 regression test 與 full test 證明修正，但沒有留下三次瀏覽器人工驗證的完整紀錄。
* [x] 每個 bug 都新增回歸測試，最終 full suite 全綠。
* [x] 三個 bug 是三個獨立 commit：

  * `edc8052 fix: correct one-based order pagination`
  * `53c6e45 fix: apply Gold discount once`
  * `4162418 fix: restore stock when cancelling orders`
* [x] 原本測試沒有抓到的原因：

  * 分頁測試沒有驗證 page 1 / page 2 的實際 offset。
  * 折扣測試只驗證計算公式，沒有把建單時的 price snapshot 與 Gold 總額串起來驗證。
  * 取消訂單測試只驗證 status，沒有驗證庫存是否補回。

#### 練習 3

* [~] `/Products/LowStock` 不帶參數已驗證 default threshold 為 `10`；`threshold=1` 已驗證 empty state。沒有另外留下 `threshold=3` 的 HTTP 紀錄，但 service test 已驗證 threshold 是嚴格 `<`，且不同門檻會改變結果。
* [~] `threshold=0` 已實際驗證為 HTTP `200`、顯示 validation error、隱藏 table；`-1` 沒有留下獨立 HTTP 紀錄，但使用同一個 `[Range(1, ...)]` 規則。
* [x] 近 30 天銷量測試包含：

  * recent + non-cancelled：計入；
  * recent + Cancelled：排除；
  * 超過 30 天：排除；
  * 非低庫存商品：不影響結果。
* [x] inactive 低庫存商品已由 service test 證明不會出現在結果。
* [x] 我沒有直接接受第一次結構。我檢查 Controller / Service / Repository / ViewModel / View 的責任，並重新比較不同 repository 分工與資料流方案後再調整。
* [x] 新增 4 個低庫存 service tests；full suite 從 `31` 增加到 `35`，全部通過。
* [x] 真實 SQL Server route smoke test 抓到並修正 InMemory 無法發現的 EF translation 問題。
* [x] 獨立 commit：`a31dcda feat(products): add low-stock inventory report`。

#### 練習 4

* [x] 重構後 OrderService tests `29/29`、full suite `35/35`。
* [x] 改善的部分：

  * `CreateOrderAsync` 只保留 orchestration。
  * request-level validation 集中到 `ValidateOrderRequest`。
  * product validation、stock mutation 與 item creation 集中到 `AddValidatedOrderItemsAsync`。
* [x] 沒有改變的部分：

  * validation order 與錯誤文字；
  * 商品錯誤的 aggregation；
  * 扣庫存與建立 snapshot 的時機；
  * public API、dependencies 與 persistence behavior。
* [x] 我有從 code review 角度看 diff，也先比較不同 helper 回傳型別的責任與耦合程度。最後讓 private item helper 只回傳 `IReadOnlyList<string>` errors，而不是回傳 `ServiceResult<Order>`，避免不必要的耦合。
* [x] 獨立 commit：`e1435aa refactor(orders): extract order creation validation`。

---

## 附錄：值得留下的對話片段

### 片段 1：我對架構有疑問時沒有直接 approve

我的 prompt：

> `why we have record LowStockProduct, inside our OrderHub.Core.Services ? why it was different then others ?`
>
> `i still feel weird about this full edit, help me verify and make the best choice of structure and future proof`

回應摘要：agent 一開始只解釋 record 是 query DTO，之後依我的追問重新檢查 dependency direction、repository ownership 與命名。最後把它改名為 `LowStockProductSummary`，並拆開 Product / Order repository 的查詢責任。

### 片段 2：比較不同方案，不直接接受第一版

我的 prompt：

> `help me verify and make the best choice of structure and future proof`

回應摘要：agent 重新檢查 Training 3 與 Training 4 的實作方式，列出各方案的責任分工、耦合程度與查詢範圍。Training 3 最後採用 Product / Order repository 分工，並把銷量查詢限制在符合門檻的 product IDs；Training 4 使用兩個 private helper，但讓 item helper 只回傳 errors，避免不必要地耦合 `ServiceResult<Order>`。

---

## 第二階段 — 自建 MCP Server

### 練習 0 — Playwright MCP

專案的 `.codex/config.toml` 已註冊 Playwright MCP。這台機器的系統 Node 是 `18.18.2`，而最新版 Playwright MCP 要求 Node 20 以上；因為機器沒有 `winget`，設定改成由 npm 暫時提供 Node 20，不更動全機 Node 安裝：

```toml
[mcp_servers.playwright]
command = "npm"
args = ["exec", "--yes", "--package=node@20", "--package=@playwright/mcp@latest", "--", "playwright-mcp"]
```

實際執行同一個命令加上 `--help` 已成功啟動 Playwright MCP CLI。建立訂單與截圖仍需要先啟動 SQL Server 和網站，這項 UI 驗證尚未完成。

### 練習 1 — 三個唯讀工具

我新增 `OrderHub.Mcp` stdio server，沒有讓工具直接存取 `OrderHubDbContext`。資料流是：

```text
MCP client
  → OrderHubTools
      → IOrderService：訂單查詢與計價
      → IProductRepository：低庫存商品查詢
```

`dotnet build src/OrderHub.Mcp/OrderHub.Mcp.csproj --no-restore -m:1` 成功，0 warnings / 0 errors。直接做 stdio discovery 時只列出 `get_order`、`low_stock`、`customer_orders`，三個工具皆為 `readOnlyHint: true`。

### 練習 2 — MCP Inspector

我使用官方 Inspector CLI，而不是只用自己寫的 JSON-RPC 腳本：

```powershell
npm exec --yes --package=node@20 --package=@modelcontextprotocol/inspector@latest -- `
  mcp-inspector --cli dotnet src/OrderHub.Mcp/bin/Debug/net8.0/OrderHub.Mcp.dll `
  --method tools/list
```

Inspector 實際列出三個工具、中文 description、參數 schema 與 read-only annotations：

* `customer_orders(customerId: integer)`
* `get_order(id: integer)`
* `low_stock(threshold: integer = 10)`

本機目前沒有可連線的 `OrderHubTraining` SQL Server，因此 `low_stock(threshold=10)` 與 `/Products` 頁面的資料比對，以及不存在訂單的實際 DB 呼叫尚未完成。server 的 tool discovery 本身不需要連線資料庫，已由 Inspector 驗證。

### 練習 3 — 註冊給 Codex

我把 OrderHub server 加到專案層級的 `.codex/config.toml`，並把預設 approval mode 設為 `writes`。因此唯讀工具可直接執行；之後加入的寫入工具則應要求確認：

```toml
[mcp_servers.orderhub]
command = "dotnet"
args = ["run", "--no-build", "--project", "src/OrderHub.Mcp"]
startup_timeout_sec = 30
default_tools_approval_mode = "writes"
```

沒有 MCP 時，這次 agent 為了回答 OrderHub 問題先搜尋 solution、閱讀 repository/service，再確認 connection string。接上 MCP 後，`low_stock(threshold=5)` 的介面已能由 server discovery 直接取得，不必重新理解資料存取程式。因本機 SQL Server 未啟動，實際的 before/after 商品清單仍待資料庫可用且重啟 Codex session 後完成。
