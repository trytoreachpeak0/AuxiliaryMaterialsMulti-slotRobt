# mes-sql 拆分 / 合并工具

需要 Node.js。首次使用：

```bash
npm install
```

| 命令 | 作用 |
|------|------|
| `npm run split` | 从根目录三个大 YAML **拆出**到子目录（会覆盖 `items/`、`tables/`、`flows/`） |
| `npm run merge` | 将子目录 **合并**为根目录 `mes-sql-catalog.yaml`、`mes-schema.yaml`、`flow-sql-map.yaml` |

日常只编辑子目录，改完执行 `npm run merge` 即可。
