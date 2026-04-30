# M8 信息不对称与失真汇报回归门禁

> 日期：2026-05-01
> 范围：后端协议、观察层、汇报层、客户端协议生成代码

## 完成范围

- 新增 `InformationReportView`，并接入 `MsgGameInit`、`MsgPlanningStart`、`MsgGameSync`。
- `ObservationStore` 支持 `clear`、`standard`、`high_distortion` 三种报告模式。
- `ObservationSnapshot` 明确携带 `ReportingMode` 和 `DirectInspection`。
- `BuildInformationReport` 从观察快照生成可审计报告元数据：可见、记忆、未知、遗漏、延迟、误读、直检标记和 notes。
- debug/full-map omniscient 视为 `clear` direct inspection，压制遗漏、延迟和误读。
- planning start、game sync、game init 均返回同一报告结构。
- 大臣观察摘要纳入 report mode、confidence、omitted/delayed/misread，避免后续大臣默认读取 raw truth。

## 验收点

- 非 omniscient 玩家仍只消费观察视图，报告会标记未知区域和记忆延迟。
- 同一观察快照可因 reporting mode 产生不同 reported 元数据。
- `high_distortion` 会降低 confidence，并产生可测试的误读/遗漏指标。
- direct inspection 强制进入 `clear`，可临时穿透或校正信息层。
- proto 源文件为唯一协议修改入口，Go 和 Unity 生成代码由 `make gen` 生成。

## 回归命令

```bash
cd server && go test -count=1 ./...
make lint
git diff --check
```

结果：全部通过。
