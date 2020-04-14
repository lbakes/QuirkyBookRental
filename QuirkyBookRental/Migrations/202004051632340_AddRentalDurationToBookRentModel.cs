namespace QuirkyBookRental.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRentalDurationToBookRentModel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BookRents", "RentalDuration", c => c.Int(nullable: false));
            AlterColumn("dbo.BookRents", "UserId", c => c.String(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BookRents", "UserId", c => c.String());
            DropColumn("dbo.BookRents", "RentalDuration");
        }
    }
}
