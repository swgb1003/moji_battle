# 大会準備の見出し修正

「大会の準備（大会中は装備固定）」を1つの見出し欄に入れていたため、補足が自動折り返しされていた。

大会モードでは「大会準備」と「（大会中は装備固定）」を別々に配置し、上下2行を中央揃えにした。上段64、下段32の文字サイズを使用。

WebGLビルド成功（UTC `2026-10-09T02:22:04.3400693Z`）。ローカルと本番ブラウザの大会準備画面で、上下2行の中央揃え、補足が折り返さず収まることを確認した。本番ブラウザのエラーレベルのログは0件。

公開先: https://moji-fighter-x.vercel.app

本番デプロイ: `https://moji-fighter-kk4h5lbf5-swgb1003-5441s-projects.vercel.app`

ローカルと本番で11ファイルの一致、gzip、MIMEを検証済み（`tournament-heading-local-delivery.json`、`tournament-heading-production-delivery.json`）。

実画面: `UI/tournament-heading-local.jpg`、`UI/tournament-heading-production.jpg`。
