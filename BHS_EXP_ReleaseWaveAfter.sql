USE [ILS]
GO
/****** Object:  StoredProcedure [dbo].[BHS_EXP_ReleaseWaveAfter]    Script Date: 8/13/2021 4:58:51 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
 Mod     | Programmer    | Date       | Modification Description
 --------------------------------------------------------------------
             | BHS3  | 10/14/2019  | Stored Procedure for exit point:Release Wave - After
*/

ALTER PROCEDURE [dbo].[BHS_EXP_ReleaseWaveAfter] (
    @ContainerID nvarchar(20)
)

AS 
    SET NOCOUNT ON; 
    
 --in case they scan the same container twice   
 DELETE FROM BHS_CUBISCAN where PALLET_ID = @ContainerID   
    
 declare @Parent nvarchar(20)
 
 select @Parent = internal_container_num from shipping_container where container_id = @ContainerID
    
INSERT INTO [ILS].[dbo].[BHS_CUBISCAN]
           ([PALLET_ID]
           ,[CARTON_ID]
           ,[ZPL_STRING]
           ,[UCC_BARCODE_NUMBER]
           ,[CARTON_QUANTITY]
           ,[CONTAINER_TYPE]
           ,[WEIGHT]
           ,[TOLERANCE]
           ,[SKU]
           ,[CUSTOMER_PO]
           ,[PROCESSED]
           ,[BARCODE_GRADE]
           ,[INTERNAL_CONTAINER_NUM]
           ,[ACTUAL_WEIGHT]
           , [ERROR])

select sc.user_def5 'Pallet ID' ,
		sc.container_id 'Carton ID', 
		null 'ZPL String', 
		icr.X_REF_ITEM 'UCC Barcode Number',
		sc.quantity 'Carton Quantity',
		sc.container_type 'Container Type',
		sc.weight 'Carton Weight', 
		sc.weight * (select user1value from generic_config_detail where record_type = 'Cubiscan_Weight_Tolerance')'Tolerance', 
		sc.item 'Sku', 
		sh.erp_order 'Customer PO',
		'N' 'Processed',
		null 'Barcode Grade',
		sc.internal_container_num 'Internal Container Number',
		null 'Actual Weight',
		null 'Error'
from shipping_container sc
	join shipment_header sh
		on sh.internal_shipment_num = sc.internal_shipment_num
	join item_cross_reference icr
		on icr.item = sc.item
where parent = @Parent and (icr.X_REF_ITEM like '10%' or icr.X_REF_ITEM like '20%')
order by sc.user_def5


 select Internal_Container_Num, Launch_Num from shipping_container where parent = @Parent



