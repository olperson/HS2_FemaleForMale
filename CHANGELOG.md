# 更新记录

## 1.2.0

- 针对复杂场景中 Timeline 对象错绑的问题，调整全场对象索引校验和轨道重绑流程。
- 加强全场骨骼就绪、GuideObject 注册及可选骨骼插件的状态检查。
- 改善 AdditionalFKNodes 复用 GuideObject 时的注册表重绑。
- 加强 NodesConstraints 与 Timeline 的独立清理、完整性校验和失败恢复。
- 加载提示随分辨率缩放，并支持额外倍率调整。

## 1.1.0

- 接入 Character Loader 1.4.2 的卡片替换入口。
- 将固定骨骼等待改为可配置的无进展超时，检测到进展时续期。

## 1.0.0

- 实现 Studio 女角色替换男角色、场景快照、骨骼迁移和失败恢复。
