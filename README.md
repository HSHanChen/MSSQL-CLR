# SQLSERVER 注册DLL文件

> 本文使用的为SQL2022版本，2017以上的都可以使用

## 注册DLL

在SQL中执行下面语句：

```tsql
CREATE ASSEMBLY Base64Functions
	FROM 'C:\your_dll_filepath\filename.dll'
	WITH PERMISSION_SET = SAFE;
GO
```

执行时可能报错：
> 针对带有 SAFE 或 EXTERNAL_ACCESS 选项的程序集“filename”的 CREATE 或 ALTER ASSEMBLY 失败，因为 sp_configure 的“CLR 严格安全性”选项设置为
> 1。Microsoft 建议使用其相应登录名具有 UNSAFE ASSEMBLY 权限的证书或非对称密钥为该程序集签名。或者，也可以使用 sp_add_trusted_assembly 信任程序集。

因为你的SQL Server 启用了“**CLR 严格安全性**”， 此时加载 **未签名的程序集**（哪怕是SAFE权限）是被禁止的

解决方案有两个：

### 方法一 使用`sp_add_trusted_assembly` 信任你的 DLL

#### 1.获取DLL的十六进制哈希（SHA-512）

在SQL中运行以下语句：

```tsql
SELECT HASHBYTES('SHA2_512', BulkColumn)
FROM OPENROWSET(BULK N'C:\your_dll_filepath\filename.dll', SINGLE_BLOB) AS BinaryData;
```

👉会得到一串128字符的16进制值（SHA-512)

#### 2.将这个哈希注册为“受信程序集”
```tsql
EXEC sp_add_trusted_assembly @hash = 0x你刚才得到的哈希, @description = N'filename';
```
> 注意：这里的哈希值，不需要用引号包裹起来

#### 3.再执行注册语句
```tsql
CREATE ASSEMBLY Base64Functions
	FROM 'C:\your_dll_filepath\filename.dll'
	WITH PERMISSION_SET = SAFE;
GO
```

### 方法二：关闭CLR严格安全性（不推荐生产环境使用）
> 如果你只是测试用途，临时开放限制也可以
```tsql
-- 启用高级选项
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;

-- 关闭 CLR 严格安全性
EXEC sp_configure 'clr strict security', 0;
RECONFIGURE;
````

## 如何验证注册成功
`SELECT * FROM sys.trusted_assemblies;`

## CLR函数
```tsql
-- 编码
CREATE FUNCTION dbo.mcFn_EncodeBase64
(
	@EnCode NVARCHAR(MAX)
)
	RETURNS NVARCHAR(MAX)
AS
	EXTERNAL NAME Base64Functions . [Base64Functions] . EncodeBase64;
GO
-- 解码
CREATE FUNCTION dbo.mcFn_DecodeBase64
(
	@EnCode NVARCHAR(MAX)
)
	RETURNS NVARCHAR(MAX)
AS
	
	EXTERNAL NAME Base64Functions . [Base64Functions] . DecodeBase64;
GO
```

## MCHR函数
```tsql
CREATE FUNCTION mcFn_Base64StrDecode
(
	@EnCode NVARCHAR(MAX)
)
	RETURNS @Result TABLE
	                (
		                DType  INT,
		                DValue NVARCHAR(MAX)
	                )
AS
BEGIN
	;
	WITH SplitInputs      AS (
		SELECT TRIM(Value) AS EncodedPart
		FROM STRING_SPLIT(@EnCode, ',')
	                         ),
	     NormalizedBase64 AS (
		SELECT EncodedPart, CASE LEN(Adjusted) % 4
			                    WHEN 2
				                    THEN Adjusted + '=='
			                    WHEN 3
				                    THEN Adjusted + '='
				                    ELSE Adjusted
		                    END AS FixedBase64
		FROM (
			SELECT EncodedPart,
			       REPLACE(REPLACE(RIGHT(EncodedPart, LEN(EncodedPart) - CHARINDEX('_', EncodedPart)),
			                       '_', '+'), '-', '/') AS Adjusted
			FROM SplitInputs
		     ) AS Fix
	                         ),
         Decoded          AS (
	         SELECT FixedBase64, dbo.mcFn_DecodeBase64(FixedBase64) AS DecodedText
	         FROM NormalizedBase64
                             )
	INSERT INTO @Result
	(
		DType, DValue
	)
	SELECT 1 AS DType, DecodedText
	FROM Decoded
	UNION ALL
	SELECT 2 AS DType,
	       SUBSTRING(DecodedText, CHARINDEX('_', DecodedText, CHARINDEX('_', DecodedText) + 1) + 1, LEN(DecodedText))
	FROM Decoded
	WHERE CHARINDEX('_', DecodedText) > 0
	  AND CHARINDEX('_', DecodedText, CHARINDEX('_', DecodedText) + 1) > 0;
	
	RETURN;
END
go
```

## 附录：DLL文件下载