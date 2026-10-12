# お題一覧のカード修正

2026-10-10 / Unity 6000.0.61f1。

- 全6カードを574×300に統一。上下段の間隔は18。
- 全カードに「挑戦する →」ボタンを配置。未クリア・クリア済みのどちらでも表示。
- 固定の01赤枠を除去。共有の赤枠がカーソルを合わせたカードへ移動し、カーソルを外すと消える。
- 対象カードの開始ボタンは朱色にフェードし、文字が白へ切り替わる。
- カードと内部の開始ボタンの間でカーソルを移しても強調表示を維持。枠はクリックを妨げない。
- キーボードの選択にも対応。メニューの動きを控えめにした場合は即時切り替え。

## 検証

ChallengeFlowTestsは重複を除く7件すべて成功。
challenge-card-hover.xmlの既存6件とchallenge-card-hover-final.xmlの入力テストで確認。
初回の入力テストは同期スクリーンショットの長いフレームがアニメーション途中の測定に影響したため失敗。撮影後のフレームを待って測定し、Unityの入力モジュールを使った実際の子要素間のポインター移動も確認して再実行。

検証内容：全カードのサイズとボタン、赤枠の移動途中と終点、開始ボタンのフェード、カーソル離脱、子ボタンへの移動、停止中のアニメーション、控えめ設定、キーボード操作、下段ボタンのレイキャストとクリック、お題の既存の対戦・条件・報酬・再挑戦・フィルター。
ユーザーの装備・設定・称号・お題データは退避して復元。
git diff --check成功。

実画面：Reports/UI/Challenges/02_list.png、12_hover_first.png、13_hover_lower.png。各880×450版も確認。
操作の検証はUnity上で実施。Web版の実ブラウザー操作検証とは区別する。

## Deploy Result

- URL: https://moji-fighter-x.vercel.app
- Deployment: https://moji-fighter-i3ksetny5-swgb1003-5441s-projects.vercel.app
- ID: dpl_2KrtZYHRQLwxkRnyPBAc1cLUuvpM
- Target: production
- Status: READY（inspectでも確認）
- Commit: e2b60e25a4a4c7b34759b032832b2829f079bfc5を基準とする未コミットのローカル変更を含む。
- Framework: Unity WebGL / static
- Build Duration: Vercel 5秒
- BuiltAtUtc: 2026-10-10T11:38:58.3295570Z
- Team: swgb1003-5441s-projects / team_T9S6jm2dWj9hQHWiULpbSwxx
- Project: moji-fighter-x / prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG

ローカルと公開URLで各11ファイルの内容一致・gzipの整合性・Wasm/JS MIME・フォントライセンスを確認。
challenge-card-hover-local-delivery.json / challenge-card-hover-production-delivery.jsonに記録。

## Post-Deploy Observability

- Error scan: 当該デプロイの直近1時間・errorレベル・上限50件。該当ログなし。
- Drains: 未確認。
- Monitoring: HTTP配信を確認。実ブラウザーのコンソールは未確認。
