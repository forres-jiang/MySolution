# 项目代码规范

## 文档和注释
- 项目中的所有文档和代码注释统一使用中文。

## C# 编码
- 类型、方法和公共属性使用 PascalCase，局部变量使用 camelCase。
- 新文件优先使用文件范围命名空间。
- 异步 I/O 使用 async/await，并向下传递 CancellationToken。
- 不使用 .Result、.Wait() 或 async void。
- 正确声明可空类型，避免用 null! 掩盖未初始化问题。
- 注释说明业务原因和约束，避免重复代码含义。
- if/else、switch、try/catch 等语句块使用大括号，即使只有一行代码。
