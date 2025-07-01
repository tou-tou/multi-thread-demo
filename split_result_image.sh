#!/bin/bash

# 画像分割スクリプト
# result.png を4つのテスト結果画像に分割する

# スクリプトの実行ディレクトリ
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DOCS_DIR="${SCRIPT_DIR}/docs"

# 入力画像と出力画像のパス
INPUT_IMAGE="${DOCS_DIR}/result.png"
OUTPUT_DIR="${DOCS_DIR}"

# 入力画像の存在確認
if [ ! -f "$INPUT_IMAGE" ]; then
    echo "エラー: ${INPUT_IMAGE} が見つかりません"
    exit 1
fi

# ImageMagickがインストールされているか確認
if ! command -v convert &> /dev/null; then
    echo "エラー: ImageMagick がインストールされていません"
    echo "インストール方法:"
    echo "  Ubuntu/Debian: sudo apt-get install imagemagick"
    echo "  macOS: brew install imagemagick"
    echo "  RHEL/CentOS: sudo yum install ImageMagick"
    exit 1
fi

echo "画像を分割しています..."

# 画像のサイズを取得
DIMENSIONS=$(identify -format "%wx%h" "$INPUT_IMAGE")
WIDTH=$(echo $DIMENSIONS | cut -d'x' -f1)
HEIGHT=$(echo $DIMENSIONS | cut -d'x' -f2)

echo "元画像サイズ: ${WIDTH}x${HEIGHT}"

# 各セクションの高さを計算
# パーセンテージから実際のピクセル値に変換
TEST1_TOP=$((HEIGHT * 9 / 100))      # 9%
TEST1_HEIGHT=$((HEIGHT * 26 / 100))  # 35% - 9% = 26%

TEST2_TOP=$((HEIGHT * 32 / 100))     # 32%
TEST2_HEIGHT=$((HEIGHT * 25 / 100))  # 57% - 32% = 25%

TEST3_TOP=$((HEIGHT * 54 / 100))     # 54%
TEST3_HEIGHT=$((HEIGHT * 26 / 100))  # 80% - 54% = 26%

TEST4_TOP=$((HEIGHT * 77 / 100))     # 77%
TEST4_HEIGHT=$((HEIGHT - TEST4_TOP)) # 100% - 77% = 23%

# テスト1: スレッドセーフでない実装
convert "$INPUT_IMAGE" -crop ${WIDTH}x${TEST1_HEIGHT}+0+${TEST1_TOP} \
    "${OUTPUT_DIR}/test1_thread_unsafe.png"
echo "✓ test1_thread_unsafe.png を作成しました"

# テスト2: Lock版の安全な実装
convert "$INPUT_IMAGE" -crop ${WIDTH}x${TEST2_HEIGHT}+0+${TEST2_TOP} \
    "${OUTPUT_DIR}/test2_lock_safe.png"
echo "✓ test2_lock_safe.png を作成しました"

# テスト3: パフォーマンス比較
convert "$INPUT_IMAGE" -crop ${WIDTH}x${TEST3_HEIGHT}+0+${TEST3_TOP} \
    "${OUTPUT_DIR}/test3_performance.png"
echo "✓ test3_performance.png を作成しました"

# テスト4: データロスト
convert "$INPUT_IMAGE" -crop ${WIDTH}x${TEST4_HEIGHT}+0+${TEST4_TOP} \
    "${OUTPUT_DIR}/test4_data_loss.png"
echo "✓ test4_data_loss.png を作成しました"

echo ""
echo "画像の分割が完了しました！"
echo "作成されたファイル:"
ls -la "${OUTPUT_DIR}"/test*.png | awk '{print "  - " $9 " (" $5 " bytes)"}'