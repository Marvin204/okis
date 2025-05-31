USE [Conexion5]
GO

-- Tabla Usuarios
CREATE TABLE [dbo].[Usuarios](
	[IdUsuario] INT IDENTITY(1,1) PRIMARY KEY,
	[Nombre] NVARCHAR(100) NOT NULL,
	[Correo] NVARCHAR(150) NOT NULL UNIQUE,
	[ContrasenaHash] NVARCHAR(256) NOT NULL
);
GO

-- Tabla MetodosNumericos
CREATE TABLE [dbo].[MetodosNumericos](
	[IdMetodo] INT IDENTITY(1,1) PRIMARY KEY,
	[NombreMetodo] NVARCHAR(100) NOT NULL
);
GO

-- Tabla Ecuaciones
CREATE TABLE [dbo].[Ecuaciones](
	[IdEcuacion] INT IDENTITY(1,1) PRIMARY KEY,
	[Funcion] NVARCHAR(255) NOT NULL,
	[ValorInicial1] FLOAT NULL,
	[ValorInicial2] FLOAT NULL,
	[ValorInicial3] FLOAT NULL,
	[Tolerancia] FLOAT NOT NULL,
	[MaxIteraciones] INT NOT NULL,
	[FechaIngreso] DATETIME NOT NULL DEFAULT GETDATE(),
	[IdUsuario] INT NOT NULL,
	[Derivada] NVARCHAR(MAX) NULL,
	[MatrizEntrada] NVARCHAR(MAX) NULL,
	FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario])
);
GO

-- Tabla Resultados
CREATE TABLE [dbo].[Resultados](
	[IdResultado] INT IDENTITY(1,1) PRIMARY KEY,
	[ResultadoFinal] FLOAT NOT NULL,
	[Iteraciones] INT NOT NULL,
	[ErrorEstimado] FLOAT NOT NULL,
	[FechaResultado] DATETIME DEFAULT GETDATE(),
	[IdEcuacion] INT NULL,
	[IdMetodo] INT NULL,
	FOREIGN KEY ([IdEcuacion]) REFERENCES [dbo].[Ecuaciones]([IdEcuacion]),
	FOREIGN KEY ([IdMetodo]) REFERENCES [dbo].[MetodosNumericos]([IdMetodo]) ON DELETE CASCADE
);
GO

-- Tabla Iteraciones
CREATE TABLE [dbo].[Iteraciones](
	[IdIteracion] INT IDENTITY(1,1) PRIMARY KEY,
	[Numero] INT NOT NULL,
	[X0] FLOAT NULL,
	[X1] FLOAT NULL,
	[X2] FLOAT NULL,
	[FX0] FLOAT NULL,
	[FX1] FLOAT NULL,
	[FX2] FLOAT NULL,
	[FXi] FLOAT NULL,
	[FXiDerivada] FLOAT NULL,
	[Resultado] FLOAT NULL,
	[Error] FLOAT NULL,
	[Variable] NVARCHAR(10) NULL,
	[IdResultado] INT NULL,
	FOREIGN KEY ([IdResultado]) REFERENCES [dbo].[Resultados]([IdResultado])
);
GO

INSERT INTO [dbo].[MetodosNumericos] ([NombreMetodo]) VALUES 
('Newton'),
('Secante'),
('Müller'),
('Gauss');
