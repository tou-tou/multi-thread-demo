---
marp: true
theme: gaia
paginate: true
#footer: 'toutou | マルチスレッドカウンター'
html: true
style: |
  section {
    font-family: 'Noto Sans JP', sans-serif;
    font-size: 28px;
  }
  section h1 {
    font-size: 2.2rem;
  }
  section h2 {
    font-size: 1.8rem;
  }
  section h3 {
    font-size: 1.4rem;
  }
  section.lead h1 {
    text-align: center;
    font-size: 2.5rem;
  }
  section.lead h3 {
    text-align: center;
    font-size: 1.3rem;
    color: #666;
  }
  code {
    font-size: 0.9em;
  }
  pre {
    font-size: 0.75em;
    line-height: 1.3;
  }
  table {
    margin: 0 auto;
    font-size: 0.85em;
  }
  table th, table td {
    padding: 6px 10px;
  }
  .highlight {
    background-color: #ffeb3b;
    padding: 2px 4px;
    border-radius: 3px;
  }
  .danger {
    color: #f44336;
    font-weight: bold;
  }
  .success {
    color: #4caf50;
    font-weight: bold;
  }
  .warning {
    color: #ff9800;
    font-weight: bold;
  }
  .small {
    font-size: 0.8em;
  }
  .smaller {
    font-size: 0.7em;
  }
---

<!-- _class: lead -->

# マルチスレッドカウンター

### 性能と正確性の探求

<br>

**発表者：toutou**

<br>


<!-- 
speaker notes:
- マルチスレッド環境でのカウンター実装の課題について説明します
- 3つの実装方法を比較し、それぞれのトレードオフを解説します
-->

---

<h1 style="font-size: 1.8rem">問題提起：メソッド呼び出しを数えたい！</h1>

<div style="font-size: 0.9em">

<h2 style="font-size: 1.4rem">要件</h2>

- 高トラフィック環境（1秒間に数千回の呼び出し）
- 複数スレッドから同時アクセス
- サーバー負荷の指標として使用

</div>

<br>

<h3 style="font-size: 1.3rem">🤔 あなたならどう実装しますか？</h3>

<!-- 
speaker notes:
- まずは素朴な実装から始めてみます
- 高頻度アクセスという要件が重要なポイントです
-->

---

<h1 style="font-size: 1.8rem">シンプルな実装を試してみる</h1>

<pre style="font-size: 0.7em"><code class="language-csharp">public class MethodCounter_NotThreadSafe : IMethodCounter
{
    private Dictionary&lt;string, int&gt; _counts = new();

    public void Record(string methodName)
    {
        if (_counts.ContainsKey(methodName))
            _counts[methodName]++;  // ← 危険！
        else
            _counts[methodName] = 1;
    }
}
</code></pre>

<br>

<h3 style="font-size: 1.2rem">⚠️ 複数スレッドから同時アクセスすると...？</h3>

---

<h1 style="font-size: 1.8rem">結果：データが壊れる 💥</h1>

<div style="font-size: 0.85em">

<h2 style="font-size: 1.3rem">テスト条件</h2>

- 100スレッド × 10,000回 = 合計100万回の呼び出し

<pre style="font-size: 0.75em"><code>スレッドセーフでない実装の問題点
--------------------------------------------------
  -> 期待したカウント: 1,000,000
  -> 実際のカウント:   112,644  ❌
  -> 例外の発生回数:   0

  [結論] マルチスレッド環境では全く信頼できません
</code></pre>

<h3 style="font-size: 1.2rem">問題の原因</h3>

- <span class="danger">データ不整合</span>: `++`操作はアトミックではない
- <span class="danger">例外リスク</span>: `Dictionary`の内部構造が破損する可能性

</div>

<!-- 
speaker notes:
- Read-Modify-Write操作の非アトミック性が原因
- 実際の本番環境では予期しない例外でシステムダウンも
-->

---

<h1 style="font-size: 1.8rem">解決策1：`lock`で守る 🔒</h1>

<h2 style="font-size: 1.4rem">相互排他による安全性の確保</h2>

<pre style="font-size: 0.65em"><code class="language-csharp">public class MethodCounter_WithLock : IMethodCounter
{
    private readonly object _lock = new();
    private Dictionary&lt;string, int&gt; _counts = new();

    public void Record(string methodName)
    {
        lock (_lock) // 一度に1スレッドのみ実行
        {
            if (_counts.ContainsKey(methodName))
                _counts[methodName]++;
            else
                _counts[methodName] = 1;
        }
    }
}
</code></pre>

<h3 style="font-size: 1.3rem">✅ これで処理は安全に！</h3>

---

<h1 style="font-size: 1.8rem">`lock`版の実行結果</h1>

<pre style="font-size: 0.75em"><code>'lock'によるスレッドセーフな実装
--------------------------------------------------
  -> 期待したカウント: 1,000,000
  -> 実際のカウント:   1,000,000  ✅

  [結論] 'lock'を使えば、安全で正確なカウンターを実装できます
</code></pre>

<br>

<h2 style="font-size: 1.5rem">🎉 問題解決！</h2>

<h3 style="font-size: 1.3rem">...でも、ちょっと待って</h3>

<p style="font-size: 1.1em"><span class="warning">スレッドの「順番待ち」がボトルネックになるのでは？</span></p>

<!-- 
speaker notes:
- 正確性は保証されたが、性能への影響が気になる
- 高頻度アクセスでは待機時間が累積する可能性
-->

---

<h1 style="font-size: 1.8rem">解決策2：Lock-Free実装 ⚡</h1>

<h2 style="font-size: 1.4rem">`ConcurrentQueue<T>`を使った高速化</h2>

<pre style="font-size: 0.65em"><code class="language-csharp">public class MethodCounter_LockFree : IMethodCounter
{
    private ConcurrentQueue&lt;string&gt; _events = new();
    
    public void Record(string methodName)
    {
        _events.Enqueue(methodName);  // lockなしで超高速！
    }
    
    public Dictionary&lt;string, int&gt; GetCountsAndReset()
    {
        // Interlocked.Exchangeでアトミックに交換
        var currentQueue = Interlocked.Exchange(
            ref _events, new ConcurrentQueue&lt;string&gt;()
        );
        // 後で集計...
    }
}
</code></pre>

---

<h1 style="font-size: 1.8rem">性能比較：驚きの結果！</h1>

<h2 style="font-size: 1.4rem">純粋な書き込み性能テスト（3秒間）</h2>

<pre style="font-size: 0.8em"><code>パフォーマンス比較結果
--------------------------------------------------
  -> 🔒 Lock版:        9,146,396 件/秒
  -> ⚡ Lock-free版:   14,775,923 件/秒

  [結論] Lock-Free版はLock版の約1.62倍高速！
</code></pre>

<br>

<h3 style="font-size: 1.4rem">🚀 これで速度も正確性も完璧...？</h3>

<!-- 
speaker notes:
- Lock-free実装により大幅な性能向上を実現
- しかし、本当に完璧なのでしょうか？
-->

---

<h1 style="font-size: 1.8rem">Lock-Free版の落とし穴 😱</h1>

<h2 style="font-size: 1.4rem">書き込みと読み出しを同時実行すると...</h2>

<pre style="font-size: 0.75em"><code>Lock-Free版の高負荷テスト結果
--------------------------------------------------
  -> 書き込み総数: 151,192,646
  -> 読み取り総数: 151,192,644
  -> ロストした数: 2  ⚠️

  [結論] わずかですが、データがロストしました！
</code></pre>

<br>

<h3 style="font-size: 1.3rem">🤔 なぜデータが失われるのか？</h3>

<p style="font-size: 0.9em">高速化と引き換えに、完全性を犠牲にしているのでしょうか？</p>

---

# データロストの仕組み

## 競合状態（Race Condition）の発生

<style scoped>
table { font-size: 0.75em; }
td { padding: 6px; font-size: 0.85em; }
</style>

<div style="font-size: 0.9em">

| 時刻 | 書込スレッドA | 読出スレッドB |
|:-----|:-------------|:-------------|
| **t1** | 現在のキュー(**Queue1**)の参照を取得 | (待機中) |
| **t2** | <span class="warning">スレッド切替 →</span> | (待機中) |
| **t3** | (停止中) | `Exchange`実行！<br>**Queue1**→**Queue2**に交換 |
| **t4** | <span class="warning">スレッド切替 →</span> | Queue1を集計中 |
| **t5** | <span class="danger">古いQueue1に書込！</span> | Queue1を集計中 |

</div>

<br>

### 💀 結果：Queue1への書き込みは永遠に失われる

<!-- 
speaker notes:
- ナノ秒単位のタイミングで発生する微妙な問題
- 参照の取得とEnqueue実行の間に割り込みが入ると発生
-->

---

# まとめ：3つの実装の比較

<style scoped>
table { 
  font-size: 0.85em;
  width: 95%;
}
th, td { 
  padding: 8px;
  text-align: center;
  font-size: 0.9em;
}
</style>

<div style="font-size: 0.9em">

| 実装方式 | 正確性 | 性能 | 適用場面 |
|:---------|:------:|:----:|:--------|
| **NotThreadSafe** | ❌ | － | 使用禁止 |
| **WithLock** | ✅ | ⚠️ | 正確性が最優先<br><span class="small">（課金、在庫管理）</span> |
| **LockFree** | ⚠️ | ✅ | 高速性が重要<br><span class="small">（アクセス解析、ログ）</span> |

</div>

<br>

<h2 style="font-size: 1.5rem">🎯 重要な教訓</h2>

<blockquote style="font-size: 1.1em; margin: 20px">
<p><strong>「銀の弾丸」は存在しない</strong></p>
<p style="font-size: 0.9em">トレードオフを理解し、要件に合った実装を選択することが重要</p>
</blockquote>

---

<!-- _class: lead -->

# ご清聴ありがとうございました

<br>

### 質問・ディスカッション歓迎！

<br>

**GitHub**: [multi-thread-demo](https://github.com/tou-tou/multi-thread-demo)
**Twitter**: [@toutou](https://twitter.com/toutou)

<!-- 
speaker notes:
- 実際のシステムでは要件に応じて使い分けが必要
- パフォーマンステストは必須
- 将来の保守性も考慮すること
-->



