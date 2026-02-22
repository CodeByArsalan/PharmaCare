USE [PharmaCareDB]
GO
/****** Object:  Table [dbo].[__EFMigrationsHistory]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountFamilies]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountFamilies](
	[AccountFamilyID] [int] IDENTITY(1,1) NOT NULL,
	[FamilyName] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_AccountFamilies] PRIMARY KEY CLUSTERED 
(
	[AccountFamilyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountHeads]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountHeads](
	[AccountHeadID] [int] IDENTITY(1,1) NOT NULL,
	[HeadName] [nvarchar](100) NOT NULL,
	[AccountFamily_ID] [int] NOT NULL,
 CONSTRAINT [PK_AccountHeads] PRIMARY KEY CLUSTERED 
(
	[AccountHeadID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Accounts]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Accounts](
	[AccountID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[AccountType_ID] [int] NOT NULL,
	[AccountHead_ID] [int] NULL,
	[AccountSubhead_ID] [int] NOT NULL,
	[IsSystemAccount] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Accounts] PRIMARY KEY CLUSTERED 
(
	[AccountID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountSubheads]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountSubheads](
	[AccountSubheadID] [int] IDENTITY(1,1) NOT NULL,
	[SubheadName] [nvarchar](100) NOT NULL,
	[AccountHead_ID] [int] NOT NULL,
 CONSTRAINT [PK_AccountSubheads] PRIMARY KEY CLUSTERED 
(
	[AccountSubheadID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountTypes]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountTypes](
	[AccountTypeID] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](20) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Description] [nvarchar](200) NULL,
 CONSTRAINT [PK_AccountTypes] PRIMARY KEY CLUSTERED 
(
	[AccountTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Categories]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Categories](
	[CategoryID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[SaleAccount_ID] [int] NULL,
	[StockAccount_ID] [int] NULL,
	[COGSAccount_ID] [int] NULL,
	[DamageAccount_ID] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED 
(
	[CategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ExpenseCategories]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ExpenseCategories](
	[ExpenseCategoryID] [int] IDENTITY(1,1) NOT NULL,
	[Parent_ID] [int] NULL,
	[Name] [nvarchar](100) NOT NULL,
	[DefaultExpenseAccount_ID] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_ExpenseCategories] PRIMARY KEY CLUSTERED 
(
	[ExpenseCategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Expenses]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Expenses](
	[ExpenseID] [int] IDENTITY(1,1) NOT NULL,
	[ExpenseCategory_ID] [int] NOT NULL,
	[SourceAccount_ID] [int] NOT NULL,
	[ExpenseAccount_ID] [int] NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[ExpenseDate] [datetime2](7) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[Reference] [nvarchar](100) NULL,
	[VendorName] [nvarchar](200) NULL,
	[Voucher_ID] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Expenses] PRIMARY KEY CLUSTERED 
(
	[ExpenseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IdentityUserClaims]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IdentityUserClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[ClaimType] [nvarchar](max) NULL,
	[ClaimValue] [nvarchar](max) NULL,
 CONSTRAINT [PK_IdentityUserClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IdentityUserLogins]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IdentityUserLogins](
	[LoginProvider] [nvarchar](128) NOT NULL,
	[ProviderKey] [nvarchar](128) NOT NULL,
	[ProviderDisplayName] [nvarchar](max) NULL,
	[UserId] [int] NOT NULL,
 CONSTRAINT [PK_IdentityUserLogins] PRIMARY KEY CLUSTERED 
(
	[LoginProvider] ASC,
	[ProviderKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IdentityUserTokens]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IdentityUserTokens](
	[UserId] [int] NOT NULL,
	[LoginProvider] [nvarchar](128) NOT NULL,
	[Name] [nvarchar](128) NOT NULL,
	[Value] [nvarchar](max) NULL,
 CONSTRAINT [PK_IdentityUserTokens] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[LoginProvider] ASC,
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Pages]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Pages](
	[PageID] [int] IDENTITY(1,1) NOT NULL,
	[Parent_ID] [int] NULL,
	[IsVisible] [bit] NOT NULL,
	[Icon] [nvarchar](50) NULL,
	[DisplayOrder] [int] NOT NULL,
	[Title] [nvarchar](100) NOT NULL,
	[Controller] [nvarchar](100) NULL,
	[Action] [nvarchar](100) NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Pages] PRIMARY KEY CLUSTERED 
(
	[PageID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PageUrls]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PageUrls](
	[PageUrlID] [int] IDENTITY(1,1) NOT NULL,
	[Page_ID] [int] NOT NULL,
	[Controller] [nvarchar](100) NOT NULL,
	[Action] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_PageUrls] PRIMARY KEY CLUSTERED 
(
	[PageUrlID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Parties]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Parties](
	[PartyID] [int] IDENTITY(1,1) NOT NULL,
	[PartyType] [nvarchar](20) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Phone] [nvarchar](20) NULL,
	[Email] [nvarchar](100) NULL,
	[Address] [nvarchar](500) NULL,
	[ContactNumber] [nvarchar](50) NULL,
	[AccountNumber] [nvarchar](50) NULL,
	[IBAN] [nvarchar](50) NULL,
	[OpeningBalance] [decimal](18, 2) NOT NULL,
	[CreditLimit] [decimal](18, 2) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
	[Account_ID] [int] NULL,
 CONSTRAINT [PK_Parties] PRIMARY KEY CLUSTERED 
(
	[PartyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Payments]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Payments](
	[PaymentID] [int] IDENTITY(1,1) NOT NULL,
	[PaymentType] [nvarchar](20) NOT NULL,
	[Party_ID] [int] NOT NULL,
	[StockMain_ID] [int] NULL,
	[Account_ID] [int] NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[PaymentDate] [datetime2](7) NOT NULL,
	[PaymentMethod] [nvarchar](20) NOT NULL,
	[Reference] [nvarchar](100) NULL,
	[ChequeNo] [nvarchar](50) NULL,
	[ChequeDate] [datetime2](7) NULL,
	[Remarks] [nvarchar](500) NULL,
	[Voucher_ID] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED 
(
	[PaymentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PriceTypes]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PriceTypes](
	[PriceTypeID] [int] IDENTITY(1,1) NOT NULL,
	[PriceTypeName] [nvarchar](100) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_PriceTypes] PRIMARY KEY CLUSTERED 
(
	[PriceTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductPrices]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductPrices](
	[ProductPriceID] [int] IDENTITY(1,1) NOT NULL,
	[Product_ID] [int] NOT NULL,
	[PriceType_ID] [int] NOT NULL,
	[SalePrice] [decimal](18, 2) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_ProductPrices] PRIMARY KEY CLUSTERED 
(
	[ProductPriceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Products]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Products](
	[ProductID] [int] IDENTITY(1,1) NOT NULL,
	[Category_ID] [int] NULL,
	[SubCategory_ID] [int] NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[ShortCode] [nvarchar](50) NULL,
	[OpeningPrice] [decimal](18, 2) NOT NULL,
	[OpeningQuantity] [int] NOT NULL,
	[UnitsInPack] [int] NOT NULL,
	[ReorderLevel] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED 
(
	[ProductID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RolePages]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RolePages](
	[RolePageID] [int] IDENTITY(1,1) NOT NULL,
	[Role_ID] [int] NOT NULL,
	[Page_ID] [int] NOT NULL,
	[CanView] [bit] NOT NULL,
	[CanCreate] [bit] NOT NULL,
	[CanEdit] [bit] NOT NULL,
	[CanDelete] [bit] NOT NULL,
 CONSTRAINT [PK_RolePages] PRIMARY KEY CLUSTERED 
(
	[RolePageID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Roles]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Roles](
	[RoleID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Description] [nvarchar](200) NULL,
	[IsSystemRole] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED 
(
	[RoleID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StockDetails]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StockDetails](
	[StockDetailID] [int] IDENTITY(1,1) NOT NULL,
	[StockMain_ID] [int] NOT NULL,
	[Product_ID] [int] NOT NULL,
	[Quantity] [decimal](18, 4) NOT NULL,
	[UnitPrice] [decimal](18, 2) NOT NULL,
	[CostPrice] [decimal](18, 2) NOT NULL,
	[DiscountPercent] [decimal](5, 2) NOT NULL,
	[DiscountAmount] [decimal](18, 2) NOT NULL,
	[LineTotal] [decimal](18, 2) NOT NULL,
	[LineCost] [decimal](18, 2) NOT NULL,
	[Remarks] [nvarchar](200) NULL,
 CONSTRAINT [PK_StockDetails] PRIMARY KEY CLUSTERED 
(
	[StockDetailID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StockMains]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StockMains](
	[StockMainID] [int] IDENTITY(1,1) NOT NULL,
	[TransactionType_ID] [int] NOT NULL,
	[TransactionNo] [nvarchar](50) NOT NULL,
	[TransactionDate] [datetime2](7) NOT NULL,
	[Party_ID] [int] NULL,
	[SubTotal] [decimal](18, 2) NOT NULL,
	[DiscountPercent] [decimal](5, 2) NOT NULL,
	[DiscountAmount] [decimal](18, 2) NOT NULL,
	[TotalAmount] [decimal](18, 2) NOT NULL,
	[PaidAmount] [decimal](18, 2) NOT NULL,
	[BalanceAmount] [decimal](18, 2) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[PaymentStatus] [nvarchar](20) NOT NULL,
	[Voucher_ID] [int] NULL,
	[ReferenceStockMain_ID] [int] NULL,
	[Remarks] [nvarchar](500) NULL,
	[VoidReason] [nvarchar](500) NULL,
	[VoidedBy] [int] NULL,
	[VoidedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_StockMains] PRIMARY KEY CLUSTERED 
(
	[StockMainID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SubCategories]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SubCategories](
	[SubCategoryID] [int] IDENTITY(1,1) NOT NULL,
	[Category_ID] [int] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
 CONSTRAINT [PK_SubCategories] PRIMARY KEY CLUSTERED 
(
	[SubCategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TransactionTypes]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TransactionTypes](
	[TransactionTypeID] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](10) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Category] [nvarchar](20) NOT NULL,
	[StockDirection] [int] NOT NULL,
	[AffectsStock] [bit] NOT NULL,
	[CreatesVoucher] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_TransactionTypes] PRIMARY KEY CLUSTERED 
(
	[TransactionTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserRoles]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserRoles](
	[UserRoleID] [int] IDENTITY(1,1) NOT NULL,
	[User_ID] [int] NOT NULL,
	[Role_ID] [int] NOT NULL,
 CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED 
(
	[UserRoleID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Users]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [int] NULL,
	[UserName] [nvarchar](256) NULL,
	[NormalizedUserName] [nvarchar](256) NULL,
	[Email] [nvarchar](256) NULL,
	[NormalizedEmail] [nvarchar](256) NULL,
	[EmailConfirmed] [bit] NOT NULL,
	[PasswordHash] [nvarchar](max) NULL,
	[SecurityStamp] [nvarchar](max) NULL,
	[ConcurrencyStamp] [nvarchar](max) NULL,
	[PhoneNumber] [nvarchar](max) NULL,
	[PhoneNumberConfirmed] [bit] NOT NULL,
	[TwoFactorEnabled] [bit] NOT NULL,
	[LockoutEnd] [datetimeoffset](7) NULL,
	[LockoutEnabled] [bit] NOT NULL,
	[AccessFailedCount] [int] NOT NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[VoucherDetails]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[VoucherDetails](
	[VoucherDetailID] [int] IDENTITY(1,1) NOT NULL,
	[Voucher_ID] [int] NOT NULL,
	[Account_ID] [int] NOT NULL,
	[DebitAmount] [decimal](18, 2) NOT NULL,
	[CreditAmount] [decimal](18, 2) NOT NULL,
	[Description] [nvarchar](200) NULL,
	[Party_ID] [int] NULL,
	[Product_ID] [int] NULL,
 CONSTRAINT [PK_VoucherDetails] PRIMARY KEY CLUSTERED 
(
	[VoucherDetailID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Vouchers]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Vouchers](
	[VoucherID] [int] IDENTITY(1,1) NOT NULL,
	[VoucherType_ID] [int] NOT NULL,
	[VoucherNo] [nvarchar](50) NOT NULL,
	[VoucherDate] [datetime2](7) NOT NULL,
	[TotalDebit] [decimal](18, 2) NOT NULL,
	[TotalCredit] [decimal](18, 2) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[SourceTable] [nvarchar](50) NULL,
	[SourceID] [int] NULL,
	[Narration] [nvarchar](500) NULL,
	[IsReversed] [bit] NOT NULL,
	[ReversedByVoucher_ID] [int] NULL,
	[ReversesVoucher_ID] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[VoidReason] [nvarchar](500) NULL,
 CONSTRAINT [PK_Vouchers] PRIMARY KEY CLUSTERED 
(
	[VoucherID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[VoucherTypes]    Script Date: 2/21/2026 11:27:13 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[VoucherTypes](
	[VoucherTypeID] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](10) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[IsAutoGenerated] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_VoucherTypes] PRIMARY KEY CLUSTERED 
(
	[VoucherTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260128072940_InitialCreate', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260128104351_RemoveSoftDelete', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260128104637_RemoveSoftDeleteFromUser', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260128153721_AddPageUrlsTable', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260129061836_RestructureAccountingHierarchy', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260129063834_FixAccountingNamingConventions', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260129104109_AddDamageAccountToCategory', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260129172054_AddPriceTypeAndProductPrice', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260130134627_AddCategoryToProductAndAccountHeadToAccount', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260130162326_ModifyPartyAndProductEntities', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260131154248_RemovePartyCode', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260131210424_ModifyProductAndStoreSchema', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260201105728_RemoveProductCode', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260201145035_SeedJournalVoucherPage', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260201181919_RemoveAccountCode', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260202095331_AddProductUnitsInPack', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260202144428_RemoveStoreLogic', N'8.0.22')
INSERT [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260203063748_AddAccountIdToParty', N'8.0.22')
GO
SET IDENTITY_INSERT [dbo].[AccountFamilies] ON 

INSERT [dbo].[AccountFamilies] ([AccountFamilyID], [FamilyName]) VALUES (1, N'ASSETS')
INSERT [dbo].[AccountFamilies] ([AccountFamilyID], [FamilyName]) VALUES (2, N'LIABILITIES')
INSERT [dbo].[AccountFamilies] ([AccountFamilyID], [FamilyName]) VALUES (3, N'CAPITAL')
INSERT [dbo].[AccountFamilies] ([AccountFamilyID], [FamilyName]) VALUES (4, N'REVENUE')
INSERT [dbo].[AccountFamilies] ([AccountFamilyID], [FamilyName]) VALUES (5, N'EXPENSE')
SET IDENTITY_INSERT [dbo].[AccountFamilies] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountHeads] ON 

INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (1, N'Curent Assets', 1)
INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (2, N'Sales', 4)
INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (3, N'Operational Expenses', 5)
INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (4, N'Owner Equity', 3)
INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (5, N'Other Income', 4)
INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (6, N'Current Liabilities', 2)
INSERT [dbo].[AccountHeads] ([AccountHeadID], [HeadName], [AccountFamily_ID]) VALUES (7, N'Cost of Goods Sold', 5)
SET IDENTITY_INSERT [dbo].[AccountHeads] OFF
GO
SET IDENTITY_INSERT [dbo].[Accounts] ON 

INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'CASH', 1, 1, 1, 1, 1, CAST(N'2026-02-12T13:08:09.8054641' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, N'HBL BANK', 2, 1, 1, 1, 1, CAST(N'2026-02-12T13:08:44.4922755' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, N'Inventory COGS Sold', 7, 7, 8, 0, 1, CAST(N'2026-02-12T13:09:46.4098107' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, N'Damaged COGS', 9, 7, 8, 0, 1, CAST(N'2026-02-12T13:10:10.7200032' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (5, N'OWNER EQUITY', 5, 4, 4, 0, 1, CAST(N'2026-02-12T13:10:31.3439140' AS DateTime2), 1, CAST(N'2026-02-12T13:14:17.9830788' AS DateTime2), 1)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (6, N'INVENTORY SALES REVENUE', 8, 2, 7, 0, 1, CAST(N'2026-02-12T13:12:40.2824666' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (7, N'INVENTORY STOCK ACCOUNT', 6, 1, 10, 0, 1, CAST(N'2026-02-12T13:13:31.5993944' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (8, N'Walk in Customer', 3, 1, 2, 0, 1, CAST(N'2026-02-12T13:33:30.9676828' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (9, N'Walk in Supplier', 4, 6, 5, 0, 1, CAST(N'2026-02-12T13:33:52.9649248' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (10, N'Shahid Iqbal', 4, 6, 5, 0, 1, CAST(N'2026-02-12T13:35:07.3634644' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Accounts] ([AccountID], [Name], [AccountType_ID], [AccountHead_ID], [AccountSubhead_ID], [IsSystemAccount], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (11, N'Amin Afridi', 3, 1, 2, 0, 1, CAST(N'2026-02-12T13:35:35.6464136' AS DateTime2), 1, NULL, NULL)
SET IDENTITY_INSERT [dbo].[Accounts] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountSubheads] ON 

INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (1, N'Cash & Banks', 1)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (2, N'Trade Receivables', 1)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (3, N'Other Payables', 6)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (4, N'Owner Equity', 4)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (5, N'Trade Payables', 6)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (6, N'Utilities', 3)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (7, N'Sales', 2)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (8, N'Cost of Goods Sold', 7)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (9, N'Office/Warehouses Rent', 3)
INSERT [dbo].[AccountSubheads] ([AccountSubheadID], [SubheadName], [AccountHead_ID]) VALUES (10, N'Inventory Stock', 1)
SET IDENTITY_INSERT [dbo].[AccountSubheads] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountTypes] ON 

INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (1, N'CASH', N'Cash Account', N'Cash on hand')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (2, N'BANK', N'Bank Account', N'Bank accounts')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (3, N'AR', N'Accounts Receivable', N'Customer balances')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (4, N'AP', N'Accounts Payable', N'Supplier balances')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (5, N'GEN', N'General', N'General Purpose')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (6, N'STK', N'Stock Account', N'Stock value')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (7, N'COGS', N'COGS Account', N'COGS Consumption')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (8, N'SALE', N'Sale Account', N'Product sales')
INSERT [dbo].[AccountTypes] ([AccountTypeID], [Code], [Name], [Description]) VALUES (9, N'DMG', N'Damaged', N'Damaged Account')
SET IDENTITY_INSERT [dbo].[AccountTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[Categories] ON 

INSERT [dbo].[Categories] ([CategoryID], [Name], [SaleAccount_ID], [StockAccount_ID], [COGSAccount_ID], [DamageAccount_ID], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'Medicines', 6, 7, 3, 4, 1, CAST(N'2026-02-12T13:28:17.0357480' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Categories] ([CategoryID], [Name], [SaleAccount_ID], [StockAccount_ID], [COGSAccount_ID], [DamageAccount_ID], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, N'Tablets', 6, 7, 3, 4, 1, CAST(N'2026-02-12T13:28:29.2559070' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Categories] ([CategoryID], [Name], [SaleAccount_ID], [StockAccount_ID], [COGSAccount_ID], [DamageAccount_ID], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, N'Syrups', 6, 7, 3, 4, 1, CAST(N'2026-02-12T13:28:40.7747865' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Categories] ([CategoryID], [Name], [SaleAccount_ID], [StockAccount_ID], [COGSAccount_ID], [DamageAccount_ID], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, N'Injections', 6, 7, 3, 4, 1, CAST(N'2026-02-12T13:29:09.4175382' AS DateTime2), 1, CAST(N'2026-02-17T15:37:15.9458491' AS DateTime2), 1)
SET IDENTITY_INSERT [dbo].[Categories] OFF
GO
SET IDENTITY_INSERT [dbo].[Pages] ON 

INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, NULL, 1, N'fas fa-user-shield', 1, N'Administration', NULL, NULL, 1, CAST(N'2026-02-11T21:30:17.1666667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, 1, 1, N'fas fa-user', 1, N'Users', N'User', N'UsersIndex', 1, CAST(N'2026-02-11T21:30:17.1700000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, 1, 1, N'fas fa-user-tag', 2, N'Roles', N'Role', N'RolesIndex', 1, CAST(N'2026-02-11T21:30:17.1700000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (10, NULL, 1, N'fas fa-cog', 2, N'Configuration', NULL, NULL, 1, CAST(N'2026-02-11T21:33:27.5433333' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (11, 10, 1, N'fas fa-users', 1, N'Parties', N'Party', N'PartiesIndex', 1, CAST(N'2026-02-11T21:33:27.5466667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (12, 10, 1, N'fas fa-tags', 2, N'Categories', N'Category', N'CategoriesIndex', 1, CAST(N'2026-02-11T21:33:27.5466667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (13, 10, 1, N'fas fa-tags', 3, N'Sub Categories', N'SubCategory', N'SubCategoriesIndex', 1, CAST(N'2026-02-11T21:33:27.5466667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (14, 10, 1, N'fas fa-pills', 4, N'Products', N'Product', N'ProductsIndex', 1, CAST(N'2026-02-11T21:33:27.5466667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (20, NULL, 1, N'fas fa-calculator', 3, N'Account Management', NULL, NULL, 1, CAST(N'2026-02-11T21:35:32.0333333' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (21, 20, 1, N'fas fa-sitemap', 1, N'Account Head', N'AccountHead', N'AccountHeadsIndex', 1, CAST(N'2026-02-11T21:35:32.0333333' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (22, 20, 1, N'fas fa-sitemap', 2, N'Account Sub Head', N'AccountSubHead', N'AccountSubHeadsIndex', 1, CAST(N'2026-02-11T21:35:32.0366667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (23, 20, 1, N'fas fa-sitemap', 3, N'Chart of Accounts', N'ChartOfAccount', N'AccountsIndex', 1, CAST(N'2026-02-11T21:35:32.0366667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (24, 20, 1, N'fas fa-book', 4, N'Journal Vouchers', N'JournalVoucher', N'JournalVoucherIndex', 1, CAST(N'2026-02-11T21:35:32.0366667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (30, NULL, 1, N'fas fa-shopping-cart', 4, N'Purchase Management', NULL, NULL, 1, CAST(N'2026-02-11T21:37:03.8766667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (31, 30, 1, N'fas fa-file-alt', 1, N'Purchase Orders', N'PurchaseOrder', N'PurchaseOrdersIndex', 1, CAST(N'2026-02-11T21:37:03.8800000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (32, 30, 1, N'fas fa-truck', 2, N'Purchases', N'Purchase', N'PurchasesIndex', 1, CAST(N'2026-02-11T21:37:03.8800000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (33, 30, 1, N'fas fa-undo', 3, N'Purchase Returns', N'PurchaseReturn', N'PurchaseReturnsIndex', 1, CAST(N'2026-02-11T21:37:03.8800000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (34, 30, 1, N'fas fa-money-bill-wave', 4, N'Supplier Payments', N'SupplierPayment', N'PaymentsIndex', 1, CAST(N'2026-02-11T21:37:03.8800000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (40, NULL, 1, N'fas fa-cash-register', 5, N'Sale Management', NULL, NULL, 1, CAST(N'2026-02-11T21:37:51.3566667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (41, 40, 1, N'fas fa-history', 1, N'Sales', N'Sale', N'SalesIndex', 1, CAST(N'2026-02-11T21:37:51.3566667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (42, 40, 1, N'fas fa-exchange-alt', 2, N'Sale Returns', N'SaleReturn', N'SaleReturnsIndex', 1, CAST(N'2026-02-11T21:37:51.3566667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (43, 40, 1, N'fas fa-money-bill-wave', 3, N'Customer Payments', N'CustomerPayment', N'ReceiptsIndex', 1, CAST(N'2026-02-11T21:37:51.3566667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (50, NULL, 1, N'fas fa-file', 6, N'Reports', NULL, NULL, 1, CAST(N'2026-02-11T21:38:41.0466667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (51, 50, 1, N'fas fa-chart-bar', 1, N'Reports', N'Report', N'Index', 1, CAST(N'2026-02-11T21:38:41.0466667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (52, 50, 1, NULL, 2, N'EndOfDay Report', N'EndOfDay', N'Index', 1, CAST(N'2026-02-19T22:36:34.7600000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (60, NULL, 1, N'fas fa-history', 7, N'Activity Log', NULL, NULL, 1, CAST(N'2026-02-11T21:39:29.7433333' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Pages] ([PageID], [Parent_ID], [IsVisible], [Icon], [DisplayOrder], [Title], [Controller], [Action], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (61, 60, 1, N'fas fa-book', 1, N'Activities', N'ActivityLog', N'Dashboard', 1, CAST(N'2026-02-11T21:39:29.7466667' AS DateTime2), 1, NULL, NULL)
SET IDENTITY_INSERT [dbo].[Pages] OFF
GO
SET IDENTITY_INSERT [dbo].[PageUrls] ON 

INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (11, 2, N'User', N'UsersIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (12, 2, N'User', N'AddUser')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (13, 2, N'User', N'EditUser')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (14, 2, N'User', N'ToggleStatus')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (15, 2, N'User', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (16, 3, N'Role', N'RolesIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (17, 3, N'Role', N'AddRole')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (18, 3, N'Role', N'EditRole')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (19, 3, N'Role', N'ToggleStatus')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (20, 3, N'Role', N'Permissions')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (21, 3, N'Role', N'SavePermissions')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (22, 12, N'Category', N'CategoriesIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (23, 12, N'Category', N'AddCategory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (24, 12, N'Category', N'EditCategory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (25, 12, N'Category', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (26, 13, N'SubCategory', N'SubCategoriesIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (27, 13, N'SubCategory', N'AddSubCategory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (28, 13, N'SubCategory', N'EditSubCategory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (29, 13, N'SubCategory', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (30, 14, N'Product', N'ProductsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (31, 14, N'Product', N'AddProduct')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (32, 14, N'Product', N'EditProduct')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (33, 14, N'Product', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (34, 14, N'Product', N'GetSubCategoriesByCategoryId')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (35, 11, N'Party', N'PartiesIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (36, 11, N'Party', N'AddParty')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (37, 11, N'Party', N'EditParty')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (38, 11, N'Party', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (39, 21, N'AccountHead', N'AccountHeadsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (40, 21, N'AccountHead', N'AddAccountHead')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (41, 21, N'AccountHead', N'EditAccountHead')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (42, 21, N'AccountHead', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (43, 22, N'AccountSubHead', N'AccountSubHeadsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (44, 22, N'AccountSubHead', N'AddAccountSubHead')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (45, 22, N'AccountSubHead', N'EditAccountSubHead')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (46, 22, N'AccountSubHead', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (47, 23, N'ChartOfAccount', N'AccountsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (48, 23, N'ChartOfAccount', N'AddAccount')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (49, 23, N'ChartOfAccount', N'EditAccount')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (50, 23, N'ChartOfAccount', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (51, 23, N'ChartOfAccount', N'GetSubHeadsByHeadId')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (52, 24, N'JournalVoucher', N'JournalVoucherIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (53, 24, N'JournalVoucher', N'AddJournalVoucher')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (54, 31, N'PurchaseOrder', N'PurchaseOrdersIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (55, 31, N'PurchaseOrder', N'AddPurchaseOrder')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (56, 31, N'PurchaseOrder', N'EditPurchaseOrder')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (57, 31, N'PurchaseOrder', N'Approve')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (58, 31, N'PurchaseOrder', N'Delete')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (59, 31, N'PurchaseOrder', N'GetProducts')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (60, 32, N'Purchase', N'PurchasesIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (61, 32, N'Purchase', N'AddPurchase')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (62, 32, N'Purchase', N'ViewPurchase')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (63, 32, N'Purchase', N'Void')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (64, 33, N'PurchaseReturn', N'PurchaseReturnsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (65, 33, N'PurchaseReturn', N'AddPurchaseReturn')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (66, 33, N'PurchaseReturn', N'ViewPurchaseReturn')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (67, 33, N'PurchaseReturn', N'Void')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (68, 33, N'PurchaseReturn', N'GetGrns')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (69, 33, N'PurchaseReturn', N'GetProducts')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (70, 34, N'SupplierPayment', N'PaymentsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (71, 34, N'SupplierPayment', N'MakePayment')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (72, 34, N'SupplierPayment', N'ViewPayment')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (73, 34, N'SupplierPayment', N'GetPaymentHistory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (74, 34, N'SupplierPayment', N'GetAccountsByType')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (75, 34, N'SupplierPayment', N'AdvancePaymentsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (76, 34, N'SupplierPayment', N'AdvancePayment')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (77, 41, N'Sale', N'SalesIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (78, 41, N'Sale', N'AddSale')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (79, 41, N'Sale', N'ViewSale')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (80, 41, N'Sale', N'Void')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (81, 41, N'Sale', N'GetProducts')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (82, 42, N'SaleReturn', N'SaleReturnsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (83, 42, N'SaleReturn', N'AddSaleReturn')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (84, 42, N'SaleReturn', N'ViewSaleReturn')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (85, 42, N'SaleReturn', N'Void')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (86, 42, N'SaleReturn', N'GetSales')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (87, 42, N'SaleReturn', N'GetProducts')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (88, 43, N'CustomerPayment', N'ReceiptsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (89, 43, N'CustomerPayment', N'PendingSales')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (90, 43, N'CustomerPayment', N'ReceivePayment')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (91, 43, N'CustomerPayment', N'ViewReceipt')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (92, 43, N'CustomerPayment', N'GetReceiptHistory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (93, 43, N'CustomerPayment', N'GetAccountsByType')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (94, 43, N'CustomerPayment', N'RefundsIndex')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (95, 43, N'CustomerPayment', N'Refund')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (96, 51, N'Report', N'Index')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (97, 51, N'Report', N'DailySalesSummary')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (98, 51, N'Report', N'SalesReport')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (99, 51, N'Report', N'SalesByProduct')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (100, 51, N'Report', N'SalesByCustomer')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (101, 51, N'Report', N'PurchaseReport')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (102, 51, N'Report', N'PurchaseBySupplier')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (103, 51, N'Report', N'CurrentStock')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (104, 51, N'Report', N'LowStock')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (105, 51, N'Report', N'ProductMovement')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (106, 51, N'Report', N'DeadStock')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (107, 51, N'Report', N'ProfitLoss')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (108, 51, N'Report', N'CashFlow')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (109, 51, N'Report', N'ReceivablesAging')
GO
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (110, 51, N'Report', N'PayablesAging')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (111, 51, N'Report', N'ExpenseReport')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (112, 51, N'Report', N'TrialBalance')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (113, 51, N'Report', N'GeneralLedger')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (114, 51, N'Report', N'CustomerLedger')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (115, 51, N'Report', N'SupplierLedger')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (116, 51, N'Report', N'CustomerBalanceSummary')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (117, 61, N'ActivityLog', N'Index')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (118, 61, N'ActivityLog', N'Details')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (119, 61, N'ActivityLog', N'EntityHistory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (120, 61, N'ActivityLog', N'UserHistory')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (121, 61, N'ActivityLog', N'Dashboard')
INSERT [dbo].[PageUrls] ([PageUrlID], [Page_ID], [Controller], [Action]) VALUES (122, 52, N'EndOfDay', N'Index')
SET IDENTITY_INSERT [dbo].[PageUrls] OFF
GO
SET IDENTITY_INSERT [dbo].[Parties] ON 

INSERT [dbo].[Parties] ([PartyID], [PartyType], [Name], [Phone], [Email], [Address], [ContactNumber], [AccountNumber], [IBAN], [OpeningBalance], [CreditLimit], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy], [Account_ID]) VALUES (1, N'Customer', N'Walk in Customer', NULL, NULL, NULL, NULL, NULL, NULL, CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-12T13:33:30.9673374' AS DateTime2), 1, NULL, NULL, 8)
INSERT [dbo].[Parties] ([PartyID], [PartyType], [Name], [Phone], [Email], [Address], [ContactNumber], [AccountNumber], [IBAN], [OpeningBalance], [CreditLimit], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy], [Account_ID]) VALUES (2, N'Supplier', N'Walk in Supplier', NULL, NULL, NULL, NULL, NULL, NULL, CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-12T13:33:52.9649206' AS DateTime2), 1, NULL, NULL, 9)
INSERT [dbo].[Parties] ([PartyID], [PartyType], [Name], [Phone], [Email], [Address], [ContactNumber], [AccountNumber], [IBAN], [OpeningBalance], [CreditLimit], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy], [Account_ID]) VALUES (3, N'Supplier', N'Shahid Iqbal', NULL, NULL, NULL, NULL, NULL, NULL, CAST(-10000.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-12T13:35:07.3634532' AS DateTime2), 1, CAST(N'2026-02-17T23:54:14.8525906' AS DateTime2), 1, 10)
INSERT [dbo].[Parties] ([PartyID], [PartyType], [Name], [Phone], [Email], [Address], [ContactNumber], [AccountNumber], [IBAN], [OpeningBalance], [CreditLimit], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy], [Account_ID]) VALUES (4, N'Customer', N'Amin Afridi', NULL, NULL, NULL, NULL, NULL, NULL, CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-12T13:35:35.6463258' AS DateTime2), 1, NULL, NULL, 11)
SET IDENTITY_INSERT [dbo].[Parties] OFF
GO
SET IDENTITY_INSERT [dbo].[PriceTypes] ON 

INSERT [dbo].[PriceTypes] ([PriceTypeID], [PriceTypeName], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'SALE PRICE', 1, CAST(N'2026-01-29T22:47:27.1600000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[PriceTypes] ([PriceTypeID], [PriceTypeName], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, N'BRANCH PRICE', 1, CAST(N'2026-01-29T22:47:27.1600000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[PriceTypes] ([PriceTypeID], [PriceTypeName], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, N'SPECIAL PRICE', 1, CAST(N'2026-01-29T22:47:27.1600000' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[PriceTypes] ([PriceTypeID], [PriceTypeName], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, N'WHOLE SALE', 1, CAST(N'2026-01-29T22:47:27.1600000' AS DateTime2), 1, NULL, NULL)
SET IDENTITY_INSERT [dbo].[PriceTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductPrices] ON 

INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, 1, 1, CAST(40.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:43:04.8778476' AS DateTime2), 1, CAST(N'2026-02-17T15:43:34.4391929' AS DateTime2), 1)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, 1, 2, CAST(30.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:43:04.9176561' AS DateTime2), 1, CAST(N'2026-02-17T15:43:34.4392165' AS DateTime2), 1)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, 1, 4, CAST(25.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:43:04.9180470' AS DateTime2), 1, CAST(N'2026-02-17T15:43:34.4392182' AS DateTime2), 1)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, 2, 1, CAST(100.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:44:23.8820900' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (5, 2, 4, CAST(80.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:44:23.8832789' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (6, 3, 1, CAST(50.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:45:31.0046768' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (7, 3, 4, CAST(40.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:45:31.0051164' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[ProductPrices] ([ProductPriceID], [Product_ID], [PriceType_ID], [SalePrice], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (9, 5, 1, CAST(30.00 AS Decimal(18, 2)), 1, CAST(N'2026-02-17T15:50:19.7436728' AS DateTime2), 1, CAST(N'2026-02-17T15:51:00.5506360' AS DateTime2), 1)
SET IDENTITY_INSERT [dbo].[ProductPrices] OFF
GO
SET IDENTITY_INSERT [dbo].[Products] ON 

INSERT [dbo].[Products] ([ProductID], [Category_ID], [SubCategory_ID], [Name], [ShortCode], [OpeningPrice], [OpeningQuantity], [UnitsInPack], [ReorderLevel], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, 4, 5, N'Dexa', N'DX', CAST(20.00 AS Decimal(18, 2)), 100, 1, 10, 1, CAST(N'2026-02-17T15:43:04.4958745' AS DateTime2), 1, CAST(N'2026-02-17T15:43:34.4180405' AS DateTime2), 1)
INSERT [dbo].[Products] ([ProductID], [Category_ID], [SubCategory_ID], [Name], [ShortCode], [OpeningPrice], [OpeningQuantity], [UnitsInPack], [ReorderLevel], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, 3, 3, N'Calpol', N'CAL', CAST(60.00 AS Decimal(18, 2)), 0, 1, 0, 1, CAST(N'2026-02-17T15:44:23.8400055' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Products] ([ProductID], [Category_ID], [SubCategory_ID], [Name], [ShortCode], [OpeningPrice], [OpeningQuantity], [UnitsInPack], [ReorderLevel], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, 2, 4, N'Panadol', N'PAN', CAST(30.00 AS Decimal(18, 2)), 500, 10, 10, 1, CAST(N'2026-02-17T15:45:30.9418410' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Products] ([ProductID], [Category_ID], [SubCategory_ID], [Name], [ShortCode], [OpeningPrice], [OpeningQuantity], [UnitsInPack], [ReorderLevel], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (5, 1, 2, N'Amoxil 500mg', N'AMX', CAST(20.00 AS Decimal(18, 2)), 0, 1, 0, 1, CAST(N'2026-02-17T15:50:19.7091100' AS DateTime2), 1, CAST(N'2026-02-17T15:51:00.5266594' AS DateTime2), 1)
SET IDENTITY_INSERT [dbo].[Products] OFF
GO
SET IDENTITY_INSERT [dbo].[RolePages] ON 

INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (1, 1, 1, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (2, 1, 2, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (3, 1, 3, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (4, 1, 10, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (5, 1, 11, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (6, 1, 12, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (7, 1, 13, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (8, 1, 14, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (9, 1, 20, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (10, 1, 21, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (11, 1, 22, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (12, 1, 23, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (13, 1, 24, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (14, 1, 30, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (15, 1, 31, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (16, 1, 32, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (17, 1, 33, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (18, 1, 34, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (19, 1, 40, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (20, 1, 41, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (21, 1, 42, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (22, 1, 43, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (23, 1, 50, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (24, 1, 51, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (25, 1, 60, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (26, 1, 61, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (37, 7, 10, 1, 0, 0, 0)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (38, 7, 20, 1, 0, 0, 0)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (40, 7, 40, 1, 0, 0, 0)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (45, 7, 11, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (46, 7, 12, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (47, 7, 13, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (48, 7, 14, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (49, 7, 21, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (50, 7, 22, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (51, 7, 23, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (52, 7, 24, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (57, 7, 41, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (58, 7, 42, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (59, 7, 43, 1, 1, 1, 0)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (62, 8, 40, 1, 0, 0, 0)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (63, 8, 41, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (64, 8, 42, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (65, 8, 43, 1, 1, 1, 1)
INSERT [dbo].[RolePages] ([RolePageID], [Role_ID], [Page_ID], [CanView], [CanCreate], [CanEdit], [CanDelete]) VALUES (66, 1, 52, 1, 1, 1, 1)
SET IDENTITY_INSERT [dbo].[RolePages] OFF
GO
SET IDENTITY_INSERT [dbo].[Roles] ON 

INSERT [dbo].[Roles] ([RoleID], [Name], [Description], [IsSystemRole], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'Administrator', N'Full system access', 1, 1, CAST(N'2026-01-28T12:40:46.6566667' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[Roles] ([RoleID], [Name], [Description], [IsSystemRole], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (7, N'Manager', N'Manage all pages', 0, 1, CAST(N'2026-02-17T13:29:33.1362403' AS DateTime2), 1, CAST(N'2026-02-17T14:22:25.9132980' AS DateTime2), 1)
INSERT [dbo].[Roles] ([RoleID], [Name], [Description], [IsSystemRole], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (8, N'Viewer', N'Only view Sales', 0, 1, CAST(N'2026-02-17T14:49:02.0106958' AS DateTime2), 1, NULL, NULL)
SET IDENTITY_INSERT [dbo].[Roles] OFF
GO
SET IDENTITY_INSERT [dbo].[SubCategories] ON 

INSERT [dbo].[SubCategories] ([SubCategoryID], [Category_ID], [Name], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, 1, N'Antibitiotics', 1, CAST(N'2026-02-12T13:29:24.2488517' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[SubCategories] ([SubCategoryID], [Category_ID], [Name], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, 1, N'Antihypertensives', 1, CAST(N'2026-02-12T13:29:29.7488766' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[SubCategories] ([SubCategoryID], [Category_ID], [Name], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, 3, N'Antihypertensives', 1, CAST(N'2026-02-12T13:29:36.2165928' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[SubCategories] ([SubCategoryID], [Category_ID], [Name], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, 2, N'Viral', 1, CAST(N'2026-02-12T13:29:44.2806040' AS DateTime2), 1, NULL, NULL)
INSERT [dbo].[SubCategories] ([SubCategoryID], [Category_ID], [Name], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (5, 4, N'Injections', 1, CAST(N'2026-02-17T13:14:07.9413504' AS DateTime2), 1, NULL, NULL)
SET IDENTITY_INSERT [dbo].[SubCategories] OFF
GO
SET IDENTITY_INSERT [dbo].[TransactionTypes] ON 

INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (1, N'PO', N'Purchase Order', N'PURCHASE', 0, 0, 0, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (2, N'GRN', N'Goods Received Note', N'PURCHASE', 1, 1, 1, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (3, N'PRTN', N'Purchase Return', N'PURCHASE', -1, 1, 1, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (4, N'SALE', N'Sale Invoice', N'SALE', -1, 1, 1, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (5, N'SRTN', N'Sales Return', N'SALE', 1, 1, 1, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (6, N'TFRO', N'Stock Transfer Out', N'INVENTORY', -1, 1, 0, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (7, N'TFRI', N'Stock Transfer In', N'INVENTORY', 1, 1, 0, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (8, N'SADJ+', N'Stock Adjustment (In)', N'INVENTORY', 1, 1, 1, 1)
INSERT [dbo].[TransactionTypes] ([TransactionTypeID], [Code], [Name], [Category], [StockDirection], [AffectsStock], [CreatesVoucher], [IsActive]) VALUES (9, N'SADJ-', N'Stock Adjustment (Out)', N'INVENTORY', -1, 1, 1, 1)
SET IDENTITY_INSERT [dbo].[TransactionTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[UserRoles] ON 

INSERT [dbo].[UserRoles] ([UserRoleID], [User_ID], [Role_ID]) VALUES (1017, 1, 1)
INSERT [dbo].[UserRoles] ([UserRoleID], [User_ID], [Role_ID]) VALUES (1018, 2, 7)
SET IDENTITY_INSERT [dbo].[UserRoles] OFF
GO
SET IDENTITY_INSERT [dbo].[Users] ON 

INSERT [dbo].[Users] ([Id], [FullName], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount]) VALUES (1, N'Arsalan', 1, CAST(N'2026-01-28T13:11:42.8132233' AS DateTime2), 0, CAST(N'2026-02-17T13:57:58.8530063' AS DateTime2), 1, N'arsalan@gmail.com', N'ARSALAN@GMAIL.COM', N'arsalan@gmail.com', N'ARSALAN@GMAIL.COM', 0, N'AQAAAAIAAYagAAAAEPoxpfU0cPtkqapT2LuZOo6Oxi98nVxOFT3FYtlDmfkW+Dz17EOXKi7o3jnxIXh7lg==', N'BPANGT7OVMWV66JGH3BISYY5TME7QYO5', N'813c0ce9-779f-41aa-a253-7a36bfecfbfd', NULL, 0, 0, NULL, 1, 0)
INSERT [dbo].[Users] ([Id], [FullName], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount]) VALUES (2, N'Amin Afridi', 1, CAST(N'2026-02-17T14:46:23.1218156' AS DateTime2), 1, NULL, NULL, N'amin@gmail.com', N'AMIN@GMAIL.COM', N'amin@gmail.com', N'AMIN@GMAIL.COM', 0, N'AQAAAAIAAYagAAAAELZ8T/ASR4TYziXtXJpNsrJp/ehg1s57kp4oBsAaXO21keLk0CjdAw3tth4up+Ym6g==', N'UMUFNQSXTTA2X6DALE3JQVTQKYXMV4CJ', N'36a1c163-2dff-42cb-a40a-2aa7801cb3d9', N'456234645245', 0, 0, NULL, 1, 0)
SET IDENTITY_INSERT [dbo].[Users] OFF
GO
SET IDENTITY_INSERT [dbo].[VoucherTypes] ON 

INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (1, N'BR', N'Bank Receipt ', 0, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (2, N'CR', N'Cash Receipt ', 0, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (3, N'BP', N'Bank Payment', 0, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (4, N'CP', N'Cash Payment ', 0, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (5, N'JV', N'Journal Voucher', 0, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (6, N'PV', N'Purchase Voucher', 1, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (7, N'PRV', N'Purchase Return Voucher', 1, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (8, N'SV', N'Sale Voucher ', 1, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (9, N'SRT', N'Sale Return Voucher', 1, 1)
INSERT [dbo].[VoucherTypes] ([VoucherTypeID], [Code], [Name], [IsAutoGenerated], [IsActive]) VALUES (10, N'DV', N'Damage Voucher', 1, 1)
SET IDENTITY_INSERT [dbo].[VoucherTypes] OFF
GO
ALTER TABLE [dbo].[Accounts] ADD  CONSTRAINT [DF__Accounts__IsSyst__4E53A1AA]  DEFAULT (CONVERT([bit],(0))) FOR [IsSystemAccount]
GO
ALTER TABLE [dbo].[Accounts] ADD  CONSTRAINT [DF__Accounts__IsActi__4F47C5E3]  DEFAULT (CONVERT([bit],(1))) FOR [IsActive]
GO
ALTER TABLE [dbo].[Products] ADD  CONSTRAINT [DF__Products__Openin__4959E263]  DEFAULT ((0)) FOR [OpeningQuantity]
GO
ALTER TABLE [dbo].[Products] ADD  CONSTRAINT [DF__Products__UnitsI__038683F8]  DEFAULT ((1)) FOR [UnitsInPack]
GO
ALTER TABLE [dbo].[Products]  WITH CHECK ADD  CONSTRAINT [CK_Products_UnitsInPack_Positive] CHECK  (([UnitsInPack]>(0)))
GO
ALTER TABLE [dbo].[Products] CHECK CONSTRAINT [CK_Products_UnitsInPack_Positive]
GO
ALTER TABLE [dbo].[AccountHeads]  WITH CHECK ADD  CONSTRAINT [FK_AccountHeads_AccountFamilies_AccountFamily_ID] FOREIGN KEY([AccountFamily_ID])
REFERENCES [dbo].[AccountFamilies] ([AccountFamilyID])
GO
ALTER TABLE [dbo].[AccountHeads] CHECK CONSTRAINT [FK_AccountHeads_AccountFamilies_AccountFamily_ID]
GO
ALTER TABLE [dbo].[Accounts]  WITH CHECK ADD  CONSTRAINT [FK_Accounts_AccountHeads_AccountHead_ID] FOREIGN KEY([AccountHead_ID])
REFERENCES [dbo].[AccountHeads] ([AccountHeadID])
GO
ALTER TABLE [dbo].[Accounts] CHECK CONSTRAINT [FK_Accounts_AccountHeads_AccountHead_ID]
GO
ALTER TABLE [dbo].[Accounts]  WITH CHECK ADD  CONSTRAINT [FK_Accounts_AccountSubheads_AccountSubhead_ID] FOREIGN KEY([AccountSubhead_ID])
REFERENCES [dbo].[AccountSubheads] ([AccountSubheadID])
GO
ALTER TABLE [dbo].[Accounts] CHECK CONSTRAINT [FK_Accounts_AccountSubheads_AccountSubhead_ID]
GO
ALTER TABLE [dbo].[Accounts]  WITH CHECK ADD  CONSTRAINT [FK_Accounts_AccountTypes_AccountType_ID] FOREIGN KEY([AccountType_ID])
REFERENCES [dbo].[AccountTypes] ([AccountTypeID])
GO
ALTER TABLE [dbo].[Accounts] CHECK CONSTRAINT [FK_Accounts_AccountTypes_AccountType_ID]
GO
ALTER TABLE [dbo].[AccountSubheads]  WITH CHECK ADD  CONSTRAINT [FK_AccountSubheads_AccountHeads_AccountHead_ID] FOREIGN KEY([AccountHead_ID])
REFERENCES [dbo].[AccountHeads] ([AccountHeadID])
GO
ALTER TABLE [dbo].[AccountSubheads] CHECK CONSTRAINT [FK_AccountSubheads_AccountHeads_AccountHead_ID]
GO
ALTER TABLE [dbo].[Categories]  WITH CHECK ADD  CONSTRAINT [FK_Categories_Accounts_COGSAccount_ID] FOREIGN KEY([COGSAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Categories] CHECK CONSTRAINT [FK_Categories_Accounts_COGSAccount_ID]
GO
ALTER TABLE [dbo].[Categories]  WITH CHECK ADD  CONSTRAINT [FK_Categories_Accounts_DamageAccount_ID] FOREIGN KEY([DamageAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Categories] CHECK CONSTRAINT [FK_Categories_Accounts_DamageAccount_ID]
GO
ALTER TABLE [dbo].[Categories]  WITH CHECK ADD  CONSTRAINT [FK_Categories_Accounts_SaleAccount_ID] FOREIGN KEY([SaleAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Categories] CHECK CONSTRAINT [FK_Categories_Accounts_SaleAccount_ID]
GO
ALTER TABLE [dbo].[Categories]  WITH CHECK ADD  CONSTRAINT [FK_Categories_Accounts_StockAccount_ID] FOREIGN KEY([StockAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Categories] CHECK CONSTRAINT [FK_Categories_Accounts_StockAccount_ID]
GO
ALTER TABLE [dbo].[ExpenseCategories]  WITH CHECK ADD  CONSTRAINT [FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID] FOREIGN KEY([DefaultExpenseAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[ExpenseCategories] CHECK CONSTRAINT [FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID]
GO
ALTER TABLE [dbo].[ExpenseCategories]  WITH CHECK ADD  CONSTRAINT [FK_ExpenseCategories_ExpenseCategories_Parent_ID] FOREIGN KEY([Parent_ID])
REFERENCES [dbo].[ExpenseCategories] ([ExpenseCategoryID])
GO
ALTER TABLE [dbo].[ExpenseCategories] CHECK CONSTRAINT [FK_ExpenseCategories_ExpenseCategories_Parent_ID]
GO
ALTER TABLE [dbo].[Expenses]  WITH CHECK ADD  CONSTRAINT [FK_Expenses_Accounts_ExpenseAccount_ID] FOREIGN KEY([ExpenseAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Expenses] CHECK CONSTRAINT [FK_Expenses_Accounts_ExpenseAccount_ID]
GO
ALTER TABLE [dbo].[Expenses]  WITH CHECK ADD  CONSTRAINT [FK_Expenses_Accounts_SourceAccount_ID] FOREIGN KEY([SourceAccount_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Expenses] CHECK CONSTRAINT [FK_Expenses_Accounts_SourceAccount_ID]
GO
ALTER TABLE [dbo].[Expenses]  WITH CHECK ADD  CONSTRAINT [FK_Expenses_ExpenseCategories_ExpenseCategory_ID] FOREIGN KEY([ExpenseCategory_ID])
REFERENCES [dbo].[ExpenseCategories] ([ExpenseCategoryID])
GO
ALTER TABLE [dbo].[Expenses] CHECK CONSTRAINT [FK_Expenses_ExpenseCategories_ExpenseCategory_ID]
GO
ALTER TABLE [dbo].[Expenses]  WITH CHECK ADD  CONSTRAINT [FK_Expenses_Vouchers_Voucher_ID] FOREIGN KEY([Voucher_ID])
REFERENCES [dbo].[Vouchers] ([VoucherID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Expenses] CHECK CONSTRAINT [FK_Expenses_Vouchers_Voucher_ID]
GO
ALTER TABLE [dbo].[IdentityUserClaims]  WITH CHECK ADD  CONSTRAINT [FK_IdentityUserClaims_Users_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[IdentityUserClaims] CHECK CONSTRAINT [FK_IdentityUserClaims_Users_UserId]
GO
ALTER TABLE [dbo].[IdentityUserLogins]  WITH CHECK ADD  CONSTRAINT [FK_IdentityUserLogins_Users_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[IdentityUserLogins] CHECK CONSTRAINT [FK_IdentityUserLogins_Users_UserId]
GO
ALTER TABLE [dbo].[IdentityUserTokens]  WITH CHECK ADD  CONSTRAINT [FK_IdentityUserTokens_Users_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[IdentityUserTokens] CHECK CONSTRAINT [FK_IdentityUserTokens_Users_UserId]
GO
ALTER TABLE [dbo].[Pages]  WITH CHECK ADD  CONSTRAINT [FK_Pages_Pages_Parent_ID] FOREIGN KEY([Parent_ID])
REFERENCES [dbo].[Pages] ([PageID])
GO
ALTER TABLE [dbo].[Pages] CHECK CONSTRAINT [FK_Pages_Pages_Parent_ID]
GO
ALTER TABLE [dbo].[PageUrls]  WITH CHECK ADD  CONSTRAINT [FK_PageUrls_Pages_Page_ID] FOREIGN KEY([Page_ID])
REFERENCES [dbo].[Pages] ([PageID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[PageUrls] CHECK CONSTRAINT [FK_PageUrls_Pages_Page_ID]
GO
ALTER TABLE [dbo].[Parties]  WITH CHECK ADD  CONSTRAINT [FK_Parties_Accounts_Account_ID] FOREIGN KEY([Account_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Parties] CHECK CONSTRAINT [FK_Parties_Accounts_Account_ID]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_Accounts_Account_ID] FOREIGN KEY([Account_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_Accounts_Account_ID]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_Parties_Party_ID] FOREIGN KEY([Party_ID])
REFERENCES [dbo].[Parties] ([PartyID])
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_Parties_Party_ID]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_StockMains_StockMain_ID] FOREIGN KEY([StockMain_ID])
REFERENCES [dbo].[StockMains] ([StockMainID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_StockMains_StockMain_ID]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_Vouchers_Voucher_ID] FOREIGN KEY([Voucher_ID])
REFERENCES [dbo].[Vouchers] ([VoucherID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_Vouchers_Voucher_ID]
GO
ALTER TABLE [dbo].[ProductPrices]  WITH CHECK ADD  CONSTRAINT [FK_ProductPrices_PriceTypes_PriceType_ID] FOREIGN KEY([PriceType_ID])
REFERENCES [dbo].[PriceTypes] ([PriceTypeID])
GO
ALTER TABLE [dbo].[ProductPrices] CHECK CONSTRAINT [FK_ProductPrices_PriceTypes_PriceType_ID]
GO
ALTER TABLE [dbo].[ProductPrices]  WITH CHECK ADD  CONSTRAINT [FK_ProductPrices_Products_Product_ID] FOREIGN KEY([Product_ID])
REFERENCES [dbo].[Products] ([ProductID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ProductPrices] CHECK CONSTRAINT [FK_ProductPrices_Products_Product_ID]
GO
ALTER TABLE [dbo].[Products]  WITH CHECK ADD  CONSTRAINT [FK_Products_Categories_Category_ID] FOREIGN KEY([Category_ID])
REFERENCES [dbo].[Categories] ([CategoryID])
GO
ALTER TABLE [dbo].[Products] CHECK CONSTRAINT [FK_Products_Categories_Category_ID]
GO
ALTER TABLE [dbo].[Products]  WITH CHECK ADD  CONSTRAINT [FK_Products_SubCategories_SubCategory_ID] FOREIGN KEY([SubCategory_ID])
REFERENCES [dbo].[SubCategories] ([SubCategoryID])
GO
ALTER TABLE [dbo].[Products] CHECK CONSTRAINT [FK_Products_SubCategories_SubCategory_ID]
GO
ALTER TABLE [dbo].[RolePages]  WITH CHECK ADD  CONSTRAINT [FK_RolePages_Pages_Page_ID] FOREIGN KEY([Page_ID])
REFERENCES [dbo].[Pages] ([PageID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RolePages] CHECK CONSTRAINT [FK_RolePages_Pages_Page_ID]
GO
ALTER TABLE [dbo].[RolePages]  WITH CHECK ADD  CONSTRAINT [FK_RolePages_Roles_Role_ID] FOREIGN KEY([Role_ID])
REFERENCES [dbo].[Roles] ([RoleID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RolePages] CHECK CONSTRAINT [FK_RolePages_Roles_Role_ID]
GO
ALTER TABLE [dbo].[StockDetails]  WITH CHECK ADD  CONSTRAINT [FK_StockDetails_Products_Product_ID] FOREIGN KEY([Product_ID])
REFERENCES [dbo].[Products] ([ProductID])
GO
ALTER TABLE [dbo].[StockDetails] CHECK CONSTRAINT [FK_StockDetails_Products_Product_ID]
GO
ALTER TABLE [dbo].[StockDetails]  WITH CHECK ADD  CONSTRAINT [FK_StockDetails_StockMains_StockMain_ID] FOREIGN KEY([StockMain_ID])
REFERENCES [dbo].[StockMains] ([StockMainID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[StockDetails] CHECK CONSTRAINT [FK_StockDetails_StockMains_StockMain_ID]
GO
ALTER TABLE [dbo].[StockMains]  WITH CHECK ADD  CONSTRAINT [FK_StockMains_Parties_Party_ID] FOREIGN KEY([Party_ID])
REFERENCES [dbo].[Parties] ([PartyID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[StockMains] CHECK CONSTRAINT [FK_StockMains_Parties_Party_ID]
GO
ALTER TABLE [dbo].[StockMains]  WITH CHECK ADD  CONSTRAINT [FK_StockMains_StockMains_ReferenceStockMain_ID] FOREIGN KEY([ReferenceStockMain_ID])
REFERENCES [dbo].[StockMains] ([StockMainID])
GO
ALTER TABLE [dbo].[StockMains] CHECK CONSTRAINT [FK_StockMains_StockMains_ReferenceStockMain_ID]
GO
ALTER TABLE [dbo].[StockMains]  WITH CHECK ADD  CONSTRAINT [FK_StockMains_TransactionTypes_TransactionType_ID] FOREIGN KEY([TransactionType_ID])
REFERENCES [dbo].[TransactionTypes] ([TransactionTypeID])
GO
ALTER TABLE [dbo].[StockMains] CHECK CONSTRAINT [FK_StockMains_TransactionTypes_TransactionType_ID]
GO
ALTER TABLE [dbo].[StockMains]  WITH CHECK ADD  CONSTRAINT [FK_StockMains_Vouchers_Voucher_ID] FOREIGN KEY([Voucher_ID])
REFERENCES [dbo].[Vouchers] ([VoucherID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[StockMains] CHECK CONSTRAINT [FK_StockMains_Vouchers_Voucher_ID]
GO
ALTER TABLE [dbo].[SubCategories]  WITH CHECK ADD  CONSTRAINT [FK_SubCategories_Categories_Category_ID] FOREIGN KEY([Category_ID])
REFERENCES [dbo].[Categories] ([CategoryID])
GO
ALTER TABLE [dbo].[SubCategories] CHECK CONSTRAINT [FK_SubCategories_Categories_Category_ID]
GO
ALTER TABLE [dbo].[UserRoles]  WITH CHECK ADD  CONSTRAINT [FK_UserRoles_Roles_Role_ID] FOREIGN KEY([Role_ID])
REFERENCES [dbo].[Roles] ([RoleID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserRoles] CHECK CONSTRAINT [FK_UserRoles_Roles_Role_ID]
GO
ALTER TABLE [dbo].[UserRoles]  WITH CHECK ADD  CONSTRAINT [FK_UserRoles_Users_User_ID] FOREIGN KEY([User_ID])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserRoles] CHECK CONSTRAINT [FK_UserRoles_Users_User_ID]
GO
ALTER TABLE [dbo].[VoucherDetails]  WITH CHECK ADD  CONSTRAINT [FK_VoucherDetails_Accounts_Account_ID] FOREIGN KEY([Account_ID])
REFERENCES [dbo].[Accounts] ([AccountID])
GO
ALTER TABLE [dbo].[VoucherDetails] CHECK CONSTRAINT [FK_VoucherDetails_Accounts_Account_ID]
GO
ALTER TABLE [dbo].[VoucherDetails]  WITH CHECK ADD  CONSTRAINT [FK_VoucherDetails_Parties_Party_ID] FOREIGN KEY([Party_ID])
REFERENCES [dbo].[Parties] ([PartyID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[VoucherDetails] CHECK CONSTRAINT [FK_VoucherDetails_Parties_Party_ID]
GO
ALTER TABLE [dbo].[VoucherDetails]  WITH CHECK ADD  CONSTRAINT [FK_VoucherDetails_Products_Product_ID] FOREIGN KEY([Product_ID])
REFERENCES [dbo].[Products] ([ProductID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[VoucherDetails] CHECK CONSTRAINT [FK_VoucherDetails_Products_Product_ID]
GO
ALTER TABLE [dbo].[VoucherDetails]  WITH CHECK ADD  CONSTRAINT [FK_VoucherDetails_Vouchers_Voucher_ID] FOREIGN KEY([Voucher_ID])
REFERENCES [dbo].[Vouchers] ([VoucherID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[VoucherDetails] CHECK CONSTRAINT [FK_VoucherDetails_Vouchers_Voucher_ID]
GO
ALTER TABLE [dbo].[Vouchers]  WITH CHECK ADD  CONSTRAINT [FK_Vouchers_Vouchers_ReversedByVoucher_ID] FOREIGN KEY([ReversedByVoucher_ID])
REFERENCES [dbo].[Vouchers] ([VoucherID])
GO
ALTER TABLE [dbo].[Vouchers] CHECK CONSTRAINT [FK_Vouchers_Vouchers_ReversedByVoucher_ID]
GO
ALTER TABLE [dbo].[Vouchers]  WITH CHECK ADD  CONSTRAINT [FK_Vouchers_Vouchers_ReversesVoucher_ID] FOREIGN KEY([ReversesVoucher_ID])
REFERENCES [dbo].[Vouchers] ([VoucherID])
GO
ALTER TABLE [dbo].[Vouchers] CHECK CONSTRAINT [FK_Vouchers_Vouchers_ReversesVoucher_ID]
GO
ALTER TABLE [dbo].[Vouchers]  WITH CHECK ADD  CONSTRAINT [FK_Vouchers_VoucherTypes_VoucherType_ID] FOREIGN KEY([VoucherType_ID])
REFERENCES [dbo].[VoucherTypes] ([VoucherTypeID])
GO
ALTER TABLE [dbo].[Vouchers] CHECK CONSTRAINT [FK_Vouchers_VoucherTypes_VoucherType_ID]
GO



