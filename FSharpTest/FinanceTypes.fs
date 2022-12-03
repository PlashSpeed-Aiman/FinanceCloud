
module FinanceTypes
    
    type FinanceCategories =
        | Food
        | Lifestyle
        | Utilities
        | Others
    type FinanceEntry =
        {EntryName:string;Amount:double;EntryDate:string;EntryMonth:int;EntryYear:int;Category:FinanceCategories}