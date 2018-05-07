namespace ClassicCars.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.AspNetUsers", newName: "Users");
            AddColumn("dbo.Users", "Credits", c => c.Int(nullable: false));
            AddColumn("dbo.Users", "SubscriptionExpires", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "SubscriptionExpires");
            DropColumn("dbo.Users", "Credits");
            RenameTable(name: "dbo.Users", newName: "AspNetUsers");
        }
    }
}
