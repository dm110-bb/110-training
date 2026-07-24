# OrderHub

練習說明入口請看 **[documents/README.md](documents/README.md)**

## 練習規則

請 fork 專案到自己的帳號進行練習。

## Fork 流程

1. 點右上角 **Fork** 建立自己帳號下的複本。

2. Clone 你 fork 出來的專案並進入目錄（把 `你的帳號` 換成你的 GitHub 帳號）：

   ```powershell
   git clone https://github.com/你的帳號/traning.git
   cd traning
   ```

3. 在你的 fork 上進行練習並 commit：

   ```powershell
   git add .
   git commit -m "你的 commit 訊息，沒訊息"
   ```

4. 推上你的 fork：

   ```powershell
   git push
   ```

## 同步原專案最新內容

當原專案 `main` 有更新時，用以下步驟把最新內容拉進你的 fork。

1. 加上原專案為 `upstream` 遠端（只需設定一次，`git remote -v` 可確認）：

   ```powershell
   git remote add upstream https://github.com/sox6769/traning.git
   ```

2. 抓取原專案最新內容並合併到本地 `main`：

   ```powershell
   git switch main
   git fetch upstream
   git merge upstream/main
   ```

   ⚠️ 若有衝突，Git 會列出衝突檔案，解完後 `git add .` 再 `git commit` 完成合併。

3. 把同步後的 `main` 推回你的 fork：

   ```powershell
   git push
   ```
