# Fast-Modding-Lib 快速模组开发库

_用于高效开发《逃离鸭科夫》模组。_

## 配置 C# 工程 / Configuring C# Project

**注意：在上传 Steam Workshop 的时候，会复写 info.ini。info.ini 中原有的信息可能会因此丢失。所以不建议在 info.ini 中存储除以上项目之外的其他信息。**

1. 在电脑上准备好《逃离鸭科夫》本体。
2. 通过 Visual Studio 软件创建一个 .Net 类库（Class Library）。
3. 配置工程参数。
   1) 框架（Target Framework）
      - **TargetFramework 建议设置为 .Net Standard 2.1**。
      - 注意删除 TargetFramework 不支持的功能，比如`<ImplicitUsings>`
   2) 添加引用（Reference Include）
      - 将《逃离鸭科夫》的`\Duckov_Data\Managed\*.dll`添加到引用中。
      - 示例：
      ```
        <ItemGroup>
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\TeamSoda.*" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ItemStatsSystem.dll" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Unity*" />
        </ItemGroup>
      ```

4. 工程配置完成！现在在你 Mod 工程的 Namespace 中编写一个 ModBehaivour 的主类。
5. 手动导入本项目构建完成的dll，dll文件可以在Steam创意工坊或本项目的Release中获取。
6. 构建工程，即可得到你的 mod 的主要 dll。然后按照上述规则说明整理好文件夹结构，即可开始本地测试。

## 使用方法
若您使用本库，请在配置完项目之后，在您的主类（ModBehaviour类）里添加以下成员：  
```
string dllPath = Assembly.GetExecutingAssembly().Location;
```
用于在后续使用中正确调取模组目录。
### 自定义游戏物品

- ItemUtils类里有多种方法，可以通过创建ItemData对象构建Item和从AssetBundle处加载Prefab两种方法。理论上支持混用。  
- 建议在复杂物品，例如武器等场合下利用AssetBundle制作物品；在简单物品，例如普通的食品等场合下使用ItemData进行制作。

### 自定义合成配方
- CraftingUtils类有创建合成配方和分解配方的方法，目前API修缮中，仅保证基本功能可用。

### 本地化
本项目提供了简单且高效的I18n类，用于方便作者一键完成从文件加载本地化。  
默认加载asset/lang下的json文件，具体见示例仓库。  
```
// 建议在Awake方法调用初始化。
void Awake()
{
    I18n.InitI18n(dllPath); // I18n初始化。
}

// 读取当前语言文件
I18n.loadFileJson(dllPath, $"/{I18n.localizedNames[SodaCraft.Localizations.LocalizationManager.CurrentLanguage]}");

```

## 示例项目
1. [Duckov FML Gun Example](https://github.com/0999312/Duckov-FML-Gun-Example)
