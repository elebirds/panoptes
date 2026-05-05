# investigate building placement and movement blocking

## Goal

排查并修复当前建筑放置/移动规则中的两个问题：1）士兵在建造完成后可能因周围建筑节点的不可重叠规则而无法正常移动；2）已解锁建筑在正常流程下仍然无法完成建造。

## What I already know

* 用户报告了两个运行时问题，均与“建筑设置规则”相关。
* 问题一表现为：士兵建造完成后被周围建筑节点困住，无法正常移动。
* 问题二表现为：多种建筑无法正常建造，即使理论上已经解锁。
* 项目是后端权威的回合制联机游戏，客户端不应自行做建造合法性校验。

## Assumptions (temporary)

* 两个问题可能都与后端对节点占用、建筑放置限制、寻路可通行性或解锁校验的规则有关。
* 至少一个问题可能同时涉及前端输入/预览与后端最终裁决的不一致。

## Open Questions

* 两个问题是否由同一套建筑占用/放置规则引起，还是分别来自寻路层和建造校验层？

## Requirements (evolving)

* 定位士兵移动被建筑节点阻塞的具体规则来源。
* 定位建筑无法完成建造的具体校验来源。
* 如存在前后端规则不一致，明确是哪一层需要修复。
* 在不破坏现有架构约束的前提下完成修复并验证。

## Acceptance Criteria (evolving)

* [ ] 能明确复现并定位士兵被建筑节点阻塞的问题根因。
* [ ] 能明确复现并定位已解锁建筑仍无法建造的问题根因。
* [ ] 修复后，相关移动/建造流程通过现有检查或新增验证。

## Definition of Done (team quality bar)

* Tests added/updated (unit/integration where appropriate)
* Lint / typecheck / CI green
* Docs/notes updated if behavior changes
* Rollout/rollback considered if risky

## Out of Scope (explicit)

* 不扩大到与本问题无关的 UI 美术、数值平衡或协议重构。

## Technical Notes

* 优先检查后端：building / game / engine / projection / staticdata。
* 视情况检查前端的建造预览、建造命令发送与移动输入链路是否与后端规则脱节。
