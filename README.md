# lilToon UnityGLTF Extensions

Ho 渲染体系的 glTF 材质导入桥，为 UnityGLTF 增加 Hollow 材质契约支持，读取 glTF 文件中的 Ho 材质契约并映射到 Unity 材质。

## 功能

- **材质契约识别**：读取 `HO_materials_principled_lil`，兼容旧扩展名 `HO_materials_openpbr_lil`。
- **导入数据保留**：将原始 JSON 与解析结果保存为材质契约子资源，并写入材质 metadata tag。
- **lilToon 映射**：根据目标 Shader 与 HoLil socket 值配置材质，自动应用不透明 / 镂空状态。
- **透明度信息保留**：记录透明模式与 RenderQueue 提示，半透明由 Unity 侧手动选择。
- **回退与排查**：可选 URP/Lit 临时映射、契约值调试，以及对已退役材质家族的明确提示。

设计说明见 [Documentation~](Documentation~/)，导入框架见 [UnityGLTF](https://github.com/KhronosGroup/UnityGLTF)。

## Ho 渲染体系相关仓库

| 仓库 | 用途 |
| --- | --- |
| [HoTools](https://github.com/HollowAmeChan/HoTools) | Blender 角色、模型、贴图、动画与自动化工具 |
| [HoUrp17.3.0](https://github.com/HollowAmeChan/HoUrp17.3.0) | URP 渲染基座 |
| [HoUrpConfig17.0.3](https://github.com/HollowAmeChan/HoUrpConfig17.0.3) | URP Shader 配置 |
| [lilToon](https://github.com/HollowAmeChan/lilToon) | 角色 / NPR 着色器 |
| [lilToon-URP-Extensions](https://github.com/HollowAmeChan/lilToon-URP-Extensions) | 光照、阴影、反射与后处理扩展 |
| [HoUnityTools](https://github.com/HollowAmeChan/HoUnityTools) | 角色、动画与创作工具 |

[HoGLTF](https://github.com/HollowAmeChan/HoGLTF) 已从 Ho 渲染体系退役，不再列为当前工作流的组成部分；原仓库仅作为历史参考。

## 可选材质合集

- [HoShader](https://github.com/HollowAmeChan/HoShader)：宝石、皮肤、绒毛等独立材质与 Shader 实验合集，按需选用。

## 日期版本与安装包

推送默认分支后自动发布日期版本（`YYYYMMDD-HHMMSS`）与 Unity 安装包；在 GitHub Releases 下载 `.tgz`，通过 Package Manager 从 tarball 安装。版本对齐、手动发布与安装说明见 [发布说明](.github/RELEASING.md)。
