using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using eLogin.Models;
using eLogin.Models.Identity;
using eLogin.Models.Roles;

namespace eLogin.Data
{
    public class DatabaseContext : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
    {
        public virtual DbSet<Audit> Audit { get; set; }
        public virtual DbSet<Customer> Customer { get; set; }
        public virtual DbSet<CustomerInfoValue> CustomerInfoValue { get; set; }
        public virtual DbSet<PasswordSettings> PasswordSettings { get; set; }
        public DbSet<PasswordHistory> PasswordHistory { get; set; }

        public virtual DbSet<CustomerLoginAttempt> CustomerLoginAttempt { get; set; }
        public virtual DbSet<CustomerPassword> CustomerPassword { get; set; }
        public virtual DbSet<IdentificationChannel> IdentificationChannel { get; set; }
        public virtual DbSet<EntityProperty> EntityProperty { get; set; }
        public virtual DbSet<ChannelEntity> ChannelEntity { get; set; }
        public virtual DbSet<ChannelLoginProperty> ChannelLoginProperty { get; set; }
        public virtual DbSet<Entity> Entity { get; set; }
        public virtual DbSet<EntityInstance> EntityInstance { get; set; }
        public virtual DbSet<EntityCategory> EntityCategory { get; set; }
        public virtual DbSet<Property> Property { get; set; }
        public virtual DbSet<Session> Session { get; set; }
        public virtual DbSet<UserSession> UserSession { get; set; }
        public virtual DbSet<SystemSetting> SystemSetting { get; set; }
        
        //public virtual DbSet<Request<HandshakeRequest>> HandshakeRequest { get; set; }
        //public virtual DbSet<Response<HandshakeResponse>> HandshakeResponse { get; set; }
        //public virtual DbSet<Request<RegisterRequest>> RegisterRequest { get; set; }
        //public virtual DbSet<Response<RegisterResponse>> RegisterResponse { get; set; }
        //public virtual DbSet<Request<LoginRequest>> LoginRequest { get; set; }
        //public virtual DbSet<Response<GeneralResponse>> GeneralResponse { get; set; }
        
        //public virtual DbSet<Request<AddCustomerEntityRequest>> AddCustomerIdentifierRequest { get; set; }
        //public virtual DbSet<Request<ResetCustomerChannelPasswordRequest>> ResetCustomerChannelPasswordRequest { get; set; }
        //public virtual DbSet<Request<UpdateCustomerChannelPasswordRequest>> UpdateCustomerChannelPasswordRequest { get; set; }
        //public virtual DbSet<Request<GetCustomerInfoRequest>> GetCustomerInfoRequest { get; set; }
        //public virtual DbSet<Request<GetCustomerInfoResponse>> GetCustomerInfoResponse { get; set; }
        //public virtual DbSet<Request<SearchCustomerByAnyValueRequest>> SearchCustomerByAnyValueRequest { get; set; }
        //public virtual DbSet<Request<SearchCustomerByPropertyValueRequest>> SearchCustomerByPropertyValueRequest { get; set; }
        //public virtual DbSet<Request<SearchCustomerResponse>> SearchCustomerResponse { get; set; }
        //public virtual DbSet<Request<CustomerLockRequest>> CustomerLockRequest { get; set; }
        //public virtual DbSet<Request<CreateCustomerEntityInstanceRequest>> CreateCustomerEntityRequest { get; set; }
        //public virtual DbSet<Request<CreateCustomerEntityInstanceResponse>> CreateCustomerEntityResponse { get; set; }
        //public virtual DbSet<Request<AddCustomerInfoRequest>> AddCustomerInfoValueRequest { get; set; }
        //public virtual DbSet<Request<UpdateCustomerEntityInstanceRequest>> UpdateCustomerEntityRequest { get; set; }
        //public virtual DbSet<Request<UpdateCustomerInfoRequest>> UpdateCustomerInfoRequest { get; set; }
        //public virtual DbSet<Request<DeleteCustomerEntityInstanceRequest>> DeleteCustomerEntityRequest { get; set; }
        //public virtual DbSet<Request<DeleteCustomerInfoRequest>> DeleteCustomerInfoRequest { get; set; }
        //public virtual DbSet<Request<GetEntityCategoriesRequest>> GetEntityCategoriesRequest { get; set; }
        //public virtual DbSet<Request<GetEntityCategoriesResponse>> GetEntityCategoriesResponse { get; set; }
        //public virtual DbSet<Request<GetEntitiesRequest>> GetEntitiesRequest { get; set; }
        //public virtual DbSet<Request<GetEntitiesResponse>> GetEntitiesResponse { get; set; }
        //public virtual DbSet<Request<GetInfoPropertiesRequest>> GetInfoPropertiesRequest { get; set; }
        //public virtual DbSet<Request<GetInfoPropertiesResponse>> GetInfoPropertiesResponse { get; set; }
        //public virtual DbSet<Request<CreateEntityCategoryRequest>> CreateEntityCategoryRequest { get; set; }
        //public virtual DbSet<Request<CreateEntityCategoryResponse>> CreateEntityCategoryResponse { get; set; }
        //public virtual DbSet<Request<CreateInfoPropertyRequest>> CreateInfoPropertyRequest { get; set; }
        //public virtual DbSet<Request<CreateInfoPropertyResponse>> CreateInfoPropertyResponse { get; set; }
        //public virtual DbSet<Request<UpdateEntityCategoryRequest>> UpdateEntityCategoryRequest { get; set; }
        //public virtual DbSet<Request<UpdateInfoPropertyRequest>> UpdateInfoPropertyRequest { get; set; }
        //public virtual DbSet<Request<DeleteEntityCategoryRequest>> DeleteEntityCategoryRequest { get; set; }
        //public virtual DbSet<Request<DeleteInfoPropertyRequest>> DeleteInfoPropertyRequest { get; set; }
        //public virtual DbSet<Request<AssignPropertyToEntityRequest>> AssignInfoPropertyToEntityCategoryRequest { get; set; }
        //public virtual DbSet<Request<GetEntityPropertiesRequest>> GetEntityCategoryInfoPropertiesRequest { get; set; }
        //public virtual DbSet<Request<GetEntityPropertiesResponse>> GetEntityCategoryInfoPropertiesResponse { get; set; }

        private Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnv;

        public DatabaseContext(DbContextOptions<DatabaseContext> options, Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
            : base(options)
        {
            this.hostingEnv = env;
        }

        private async Task Auditor(string User = "System")
        {
            var Audits = new List<Audit>();

            foreach (var Entry in ChangeTracker.Entries())
            {
                if(Entry.Entity.GetType().Name != "Audit")
                {
                    bool IsSoftDelete = false;
                    foreach (var Property in Entry.Properties)
                    {
                        if (Property.Metadata.Name == "IsDeleted")
                        {
                            if (Property.CurrentValue?.ToString() == "True")
                                IsSoftDelete = true;
                        }
                    }
                    foreach (var Property in Entry.Properties)
                    {
                        switch (Entry.State)
                        {
                            case EntityState.Added:
                                {
                                    Audits.Add(new Audit()
                                    {
                                        action = Entry.State.ToString(),
                                        tableName = Entry.Entity.GetType().Name.Replace("Proxy",""),
                                        recordId = (Guid)Entry.CurrentValues["Id"],
                                        parameter = Property.Metadata.Name,
                                        fromValue = null,
                                        toValue = Property.CurrentValue?.ToString(),
                                        performedBy = User,
                                        dateTime = DateTime.UtcNow
                                    });

                                    break;
                                }
                            case EntityState.Modified:
                                {
                                    if (Entry.GetDatabaseValues().GetValue<object>(Property.Metadata.Name)?.ToString() ==
                                        Property.CurrentValue?.ToString()) continue;
                                    string Action = Entry.State.ToString();
                                    if (IsSoftDelete) Action = "Soft Delete";

                                    Audits.Add(new Audit()
                                    {
                                        action = Action,
                                        tableName = Entry.Entity.GetType().Name.Replace("Proxy", ""),
                                        recordId = (Guid)Entry.CurrentValues["Id"],
                                        parameter = Property.Metadata.Name,
                                        fromValue = Entry.GetDatabaseValues().GetValue<object>(Property.Metadata.Name)?.ToString(),
                                        toValue = Property.CurrentValue?.ToString(),
                                        performedBy = User,
                                        dateTime = DateTime.UtcNow
                                    });

                                    break;
                                }
                            case EntityState.Deleted:
                                {
                                    Audits.Add(new Audit()
                                    {
                                        action = Entry.State.ToString(),
                                        tableName = Entry.Entity.GetType().Name.Replace("Proxy", ""),
                                        recordId = (Guid)Entry.CurrentValues["Id"],
                                        parameter = Property.Metadata.Name,
                                        fromValue = Entry.GetDatabaseValues().GetValue<object>(Property.Metadata.Name)?.ToString(),
                                        toValue = null,
                                        performedBy = User,
                                        dateTime = DateTime.UtcNow
                                    });

                                    break;
                                }
                            default:
                                continue;
                        }
                    }
                }
                
            }

            await Audit.AddRangeAsync(Audits);
        }

        public async Task<int> SaveChangesAsync(string User)
        {
            
            //bool isLicenseValid = await IsLicenseValid();
            //if(!isLicenseValid)
            //{
            //    return 0;
            //}
            await Auditor(User);

            return await base.SaveChangesAsync();
        }

        


        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await Auditor();

            return await base.SaveChangesAsync(cancellationToken);
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>()
                           .HasMany(u => u.PasswordHistories)
                           .WithOne(ph => ph.User)
                           .HasForeignKey(ph => ph.UserId)
                           .IsRequired();
            modelBuilder.Entity<PasswordHistory>()
                      .HasOne(p => p.User)
                      .WithMany(u => u.PasswordHistories)
                      .HasForeignKey(p => p.UserId);
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<Role>().Property(Property => Property.Id).HasColumnName("RoleId");

            modelBuilder.Entity<RoleClaim>().ToTable("RoleClaims");
            modelBuilder.Entity<RoleClaim>().Property(Property => Property.Id).HasColumnName("RoleClaimId");

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<User>().Property(Property => Property.Id).HasColumnName("UserId");

            modelBuilder.Entity<UserRole>().ToTable("UserRoles");
            modelBuilder.Entity<UserRole>().Property(Property => Property.RoleId).HasColumnName("RoleId");
            modelBuilder.Entity<UserRole>().Property(Property => Property.UserId).HasColumnName("UserId");

            modelBuilder.Entity<UserLogin>().ToTable("UserLogins");
            modelBuilder.Entity<UserLogin>().Property(Property => Property.UserId).HasColumnName("UserId");

            modelBuilder.Entity<UserClaim>().ToTable("UserClaims");
            modelBuilder.Entity<UserClaim>().Property(Property => Property.Id).HasColumnName("UserClaimId");
            modelBuilder.Entity<UserClaim>().Property(Property => Property.UserId).HasColumnName("UserId");

            modelBuilder.Entity<UserToken>().ToTable("UserTokens");
            modelBuilder.Entity<UserToken>().Property(Property => Property.UserId).HasColumnName("UserId");



            modelBuilder.Entity<Role>()
                            .HasData(
                                new Role
                                {
                                    Id = Guid.NewGuid(),
                                    Name = nameof(eLoginAdmin),
                                    NormalizedName = nameof(eLoginAdmin).ToUpper()
                                },
                                new Role
                                {
                                    Id = Guid.NewGuid(),
                                    Name = nameof(eLoginAgent),
                                    NormalizedName = nameof(eLoginAgent).ToUpper()
                                },
                                new Role
                                {
                                    Id = Guid.NewGuid(),
                                    Name = nameof(eLoginGuest),
                                    NormalizedName = nameof(eLoginGuest).ToUpper()
                                });
            //modelBuilder.Entity<AddCustomerEntityRequest>(entity =>
            //{
            //    entity.ToTable("AddCustomerEntityRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.AddCustomerEntityRequest).HasForeignKey<AddCustomerEntityRequest>(d => d.Id);
            //});
            //modelBuilder.Entity<Request<HandshakeRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<HandshakeRequest>(o => o.Id);
            //});
            //modelBuilder.Entity<Response<HandshakeResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<HandshakeResponse>(o => o.Id);
            //});
            //modelBuilder.Entity<Request<RegisterRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<RegisterRequest>(o => o.Id);
            //});
            //modelBuilder.Entity<Response<RegisterResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<RegisterResponse>(o => o.Id);
            //}); modelBuilder.Entity<Request<LoginRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<LoginRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<GeneralResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GeneralResponse>(o => o.Id);
            
            //});
            //modelBuilder.Entity<Request<AddCustomerEntityRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload)
            //        .WithOne()
            //        .HasForeignKey<Request<AddCustomerEntityRequest>>(o => o.Id);
            //});

            //modelBuilder.Entity<Request<ResetCustomerChannelPasswordRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<ResetCustomerChannelPasswordRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<UpdateCustomerChannelPasswordRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<UpdateCustomerChannelPasswordRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<GetCustomerInfoRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetCustomerInfoRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<GetCustomerInfoResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetCustomerInfoResponse>(o => o.Id);
            //}); modelBuilder.Entity<Request<SearchCustomerByAnyValueRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<SearchCustomerByAnyValueRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<SearchCustomerByPropertyValueRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<SearchCustomerByPropertyValueRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<SearchCustomerResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<SearchCustomerResponse>(o => o.Id);
            //}); modelBuilder.Entity<Request<CustomerLockRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CustomerLockRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<CreateCustomerEntityInstanceRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CreateCustomerEntityInstanceRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<CreateCustomerEntityInstanceResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CreateCustomerEntityInstanceResponse>(o => o.Id);
            //}); modelBuilder.Entity<Request<AddCustomerInfoRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<AddCustomerInfoRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<UpdateCustomerEntityInstanceRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<UpdateCustomerEntityInstanceRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<UpdateCustomerInfoRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<UpdateCustomerInfoRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<DeleteCustomerEntityInstanceRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<DeleteCustomerEntityInstanceRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<DeleteCustomerInfoRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<DeleteCustomerInfoRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<GetEntityCategoriesRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetEntityCategoriesRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<GetEntityCategoriesResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetEntityCategoriesResponse>(o => o.Id);
            //}); modelBuilder.Entity<Request<GetInfoPropertiesRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetInfoPropertiesRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<GetInfoPropertiesResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetInfoPropertiesResponse>(o => o.Id);
            //}); modelBuilder.Entity<Request<CreateEntityCategoryRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CreateEntityCategoryRequest>(o => o.Id);
            //}); 
            //modelBuilder.Entity<Response<CreateEntityCategoryResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CreateEntityCategoryResponse>(o => o.Id);
            //}); 
            //modelBuilder.Entity<Request<CreateInfoPropertyRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CreateInfoPropertyRequest>(o => o.Id);
            //});
            //modelBuilder.Entity<Response<CreateInfoPropertyResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<CreateInfoPropertyResponse>(o => o.Id);
            //}); 
            //modelBuilder.Entity<Request<UpdateEntityCategoryRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<UpdateEntityCategoryRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<UpdateInfoPropertyRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<UpdateInfoPropertyRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<DeleteEntityCategoryRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<DeleteEntityCategoryRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<DeleteInfoPropertyRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<DeleteInfoPropertyRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<AssignPropertyToEntityRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<AssignPropertyToEntityRequest>(o => o.Id);
            //}); modelBuilder.Entity<Request<GetEntityPropertiesRequest>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetEntityPropertiesRequest>(o => o.Id);
            //}); modelBuilder.Entity<Response<GetEntityPropertiesResponse>>(ob =>
            //{
            //    ob.HasOne(o => o.Payload).WithOne()
            //        .HasForeignKey<GetEntityPropertiesResponse>(o => o.Id);
            //});
            //modelBuilder.Entity<AddCustomerIdentifierRequest>(entity =>
            //{
            //    entity.ToTable("AddCustomerIdentifierRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<AddCustomerInfoRequest>(entity =>
            //{
            //    entity.ToTable("AddCustomerInfoRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.Value).IsRequired();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.AddCustomerInfoRequest).HasForeignKey<AddCustomerInfoRequest>(d => d.Id);
            //});

            //modelBuilder.Entity<AddCustomerInfoValueRequest>(entity =>
            //{
            //    entity.ToTable("AddCustomerInfoValueRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<AssignInfoPropertyToEntityCategoryRequest>(entity =>
            //{
            //    entity.ToTable("AssignInfoPropertyToEntityCategoryRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<AssignPropertyToEntityRequest>(entity =>
            //{
            //    entity.ToTable("AssignPropertyToEntityRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.AssignPropertyToEntityRequest).HasForeignKey<AssignPropertyToEntityRequest>(d => d.Id);
            //});

          
            modelBuilder.Entity<ChannelEntity>(entity =>
            {
                entity.ToTable("ChannelEntity");

                entity.HasIndex(e => e.EntityId, "IX_ChannelEntity_EntityId");

                entity.HasIndex(e => e.IdentificationChannelId, "IX_ChannelEntity_IdentificationChannelId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.Entity).WithMany(p => p.ChannelEntities)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.IdentificationChannel).WithMany(p => p.ChannelEntities)
                    .HasForeignKey(d => d.IdentificationChannelId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<ChannelLoginProperty>(entity =>
            {
                entity.ToTable("ChannelLoginProperty");

                entity.HasIndex(e => e.IdentificationChannelId, "IX_ChannelLoginProperty_IdentificationChannelId");

                entity.HasIndex(e => e.PropertyId, "IX_ChannelLoginProperty_PropertyId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.IdentificationChannel).WithMany(p => p.ChannelLoginProperties)
                    .HasForeignKey(d => d.IdentificationChannelId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.Property).WithMany(p => p.ChannelLoginProperties)
                    .HasForeignKey(d => d.PropertyId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            //modelBuilder.Entity<CreateCustomerEntityInstanceRequest>(entity =>
            //{
            //    entity.ToTable("CreateCustomerEntityInstanceRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EntityInstanceName).IsRequired();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.CreateCustomerEntityInstanceRequest).HasForeignKey<CreateCustomerEntityInstanceRequest>(d => d.Id);
            //});

            //modelBuilder.Entity<CreateCustomerEntityInstanceResponse>(entity =>
            //{
            //    entity.ToTable("CreateCustomerEntityInstanceResponse");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.CreateCustomerEntityInstanceResponse).HasForeignKey<CreateCustomerEntityInstanceResponse>(d => d.Id);
            //});

            //modelBuilder.Entity<CreateCustomerEntityRequest>(entity =>
            //{
            //    entity.ToTable("CreateCustomerEntityRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<CreateCustomerEntityResponse>(entity =>
            //{
            //    entity.ToTable("CreateCustomerEntityResponse");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<CreateEntityCategoryRequest>(entity =>
            //{
            //    entity.ToTable("CreateEntityCategoryRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

           

            //modelBuilder.Entity<CreateEntityCategoryResponse1>(entity =>
            //{
            //    entity.ToTable("CreateEntityCategoryResponse1");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.CreateEntityCategoryResponse1).HasForeignKey<CreateEntityCategoryResponse1>(d => d.Id);
            //});

            //modelBuilder.Entity<CreateInfoPropertyRequest>(entity =>
            //{
            //    entity.ToTable("CreateInfoPropertyRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

          

            //modelBuilder.Entity<CreateInfoPropertyResponse1>(entity =>
            //{
            //    entity.ToTable("CreateInfoPropertyResponse1");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.CreateInfoPropertyResponse1).HasForeignKey<CreateInfoPropertyResponse1>(d => d.Id);
            //});

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customer");

                entity.Property(e => e.Id).ValueGeneratedNever();
            });

         

            modelBuilder.Entity<CustomerInfoValue>(entity =>
            {
                entity.ToTable("CustomerInfoValue");

                entity.HasIndex(e => e.CustomerId, "IX_CustomerInfoValue_CustomerId");

                entity.HasIndex(e => e.EntityInstanceId, "IX_CustomerInfoValue_EntityInstanceId");

                entity.HasIndex(e => e.PropertyId, "IX_CustomerInfoValue_PropertyId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.Customer).WithMany(p => p.CustomerInfoValues)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.EntityInstance).WithMany(p => p.CustomerInfoValues)
                    .HasForeignKey(d => d.EntityInstanceId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.Property).WithMany(p => p.CustomerInfoValues)
                    .HasForeignKey(d => d.PropertyId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<CustomerLockRequest>(entity =>
            {
                entity.ToTable("CustomerLockRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestCustomerLockRequestDateTime).HasColumnName("Request<CustomerLockRequest>_DateTime");
            });

            modelBuilder.Entity<CustomerLoginAttempt>(entity =>
            {
                entity.ToTable("CustomerLoginAttempt");

                entity.HasIndex(e => e.CustomerId, "IX_CustomerLoginAttempt_CustomerId");

                entity.HasIndex(e => e.IdentificationChannelId, "IX_CustomerLoginAttempt_IdentificationChannelId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.Customer).WithMany(p => p.CustomerLoginAttempts)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.IdentificationChannel).WithMany(p => p.CustomerLoginAttempts)
                    .HasForeignKey(d => d.IdentificationChannelId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<CustomerPassword>(entity =>
            {
                entity.ToTable("CustomerPassword");

                entity.HasIndex(e => e.CustomerId, "IX_CustomerPassword_CustomerId");

                entity.HasIndex(e => e.IdentificationChannelId, "IX_CustomerPassword_IdentificationChannelId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.Customer).WithMany(p => p.CustomerPasswords)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.IdentificationChannel).WithMany(p => p.CustomerPasswords)
                    .HasForeignKey(d => d.IdentificationChannelId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            //modelBuilder.Entity<DeleteCustomerEntityInstanceRequest>(entity =>
            //{
            //    entity.ToTable("DeleteCustomerEntityInstanceRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.DeleteCustomerEntityInstanceRequest).HasForeignKey<DeleteCustomerEntityInstanceRequest>(d => d.Id);
            //});

            //modelBuilder.Entity<DeleteCustomerEntityRequest>(entity =>
            //{
            //    entity.ToTable("DeleteCustomerEntityRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            modelBuilder.Entity<DeleteCustomerInfoRequest>(entity =>
            {
                entity.ToTable("DeleteCustomerInfoRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestDeleteCustomerInfoRequestDateTime).HasColumnName("Request<DeleteCustomerInfoRequest>_DateTime");
            });

            modelBuilder.Entity<DeleteEntityCategoryRequest>(entity =>
            {
                entity.ToTable("DeleteEntityCategoryRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestDeleteEntityCategoryRequestDateTime).HasColumnName("Request<DeleteEntityCategoryRequest>_DateTime");
            });

            modelBuilder.Entity<DeleteInfoPropertyRequest>(entity =>
            {
                entity.ToTable("DeleteInfoPropertyRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestDeleteInfoPropertyRequestDateTime).HasColumnName("Request<DeleteInfoPropertyRequest>_DateTime");
            });

            modelBuilder.Entity<Entity>(entity =>
            {
                entity.ToTable("Entity");

                entity.HasIndex(e => e.EntityCategoryId, "IX_Entity_EntityCategoryId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.EntityCategory).WithMany(p => p.Entities)
                    .HasForeignKey(d => d.EntityCategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

         

            modelBuilder.Entity<EntityInstance>(entity =>
            {
                entity.ToTable("EntityInstance");

                entity.HasIndex(e => e.CustomerId, "IX_EntityInstance_CustomerId");

                entity.HasIndex(e => e.EntityId, "IX_EntityInstance_EntityId");

                entity.Property(e => e.Id).ValueGeneratedNever();

              

                entity.HasOne(d => d.Entity).WithMany(p => p.EntityInstances)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

       

            modelBuilder.Entity<EntityProperty>(entity =>
            {
                entity.ToTable("EntityProperty");

                entity.HasIndex(e => e.EntityId, "IX_EntityProperty_EntityId");

                entity.HasIndex(e => e.PropertyId, "IX_EntityProperty_PropertyId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.Entity).WithMany(p => p.EntityProperties)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.Property).WithMany(p => p.EntityProperties)
                    .HasForeignKey(d => d.PropertyId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<GeneralResponse>(entity =>
            {
                entity.ToTable("GeneralResponse");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.ResponseGeneralResponseDateTime).HasColumnName("Response<GeneralResponse>_DateTime");
                entity.Property(e => e.ResponseGeneralResponseRequestId).HasColumnName("Response<GeneralResponse>_RequestId");
            });

            modelBuilder.Entity<GetCustomerInfoRequest>(entity =>
            {
                entity.ToTable("GetCustomerInfoRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestGetCustomerInfoRequestDateTime).HasColumnName("Request<GetCustomerInfoRequest>_DateTime");
            });

           

            //modelBuilder.Entity<GetCustomerInfoResponse1>(entity =>
            //{
            //    entity.ToTable("GetCustomerInfoResponse1");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.GetCustomerInfoResponse1).HasForeignKey<GetCustomerInfoResponse1>(d => d.Id);
            //});

       

         

            modelBuilder.Entity<GetEntityCategoriesRequest>(entity =>
            {
                entity.ToTable("GetEntityCategoriesRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestGetEntityCategoriesRequestDateTime).HasColumnName("Request<GetEntityCategoriesRequest>_DateTime");
            });

          

            //modelBuilder.Entity<GetEntityCategoriesResponse1>(entity =>
            //{
            //    entity.ToTable("GetEntityCategoriesResponse1");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.GetEntityCategoriesResponse1).HasForeignKey<GetEntityCategoriesResponse1>(d => d.Id);
            //});

            modelBuilder.Entity<GetEntityCategoryInfoPropertiesRequest>(entity =>
            {
                entity.ToTable("GetEntityCategoryInfoPropertiesRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
            });

            modelBuilder.Entity<GetEntityCategoryInfoPropertiesResponse>(entity =>
            {
                entity.ToTable("GetEntityCategoryInfoPropertiesResponse");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
            });

            //modelBuilder.Entity<GetEntityPropertiesRequest>(entity =>
            //{
            //    entity.ToTable("GetEntityPropertiesRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.GetEntityPropertiesRequest).HasForeignKey<GetEntityPropertiesRequest>(d => d.Id);
            //});

            //modelBuilder.Entity<GetEntityPropertiesResponse>(entity =>
            //{
            //    entity.ToTable("GetEntityPropertiesResponse");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.GetEntityPropertiesResponse).HasForeignKey<GetEntityPropertiesResponse>(d => d.Id);
            //});

            modelBuilder.Entity<GetInfoPropertiesRequest>(entity =>
            {
                entity.ToTable("GetInfoPropertiesRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestGetInfoPropertiesRequestDateTime).HasColumnName("Request<GetInfoPropertiesRequest>_DateTime");
            });

           

            //modelBuilder.Entity<GetInfoPropertiesResponse1>(entity =>
            //{
            //    entity.ToTable("GetInfoPropertiesResponse1");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.GetInfoPropertiesResponse1).HasForeignKey<GetInfoPropertiesResponse1>(d => d.Id);
            //});

            modelBuilder.Entity<HandshakeRequest>(entity =>
            {
                entity.ToTable("HandshakeRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestHandshakeRequestChannelId).HasColumnName("Request<HandshakeRequest>_ChannelId");
                entity.Property(e => e.RequestHandshakeRequestDateTime).HasColumnName("Request<HandshakeRequest>_DateTime");
            });

            modelBuilder.Entity<HandshakeResponse>(entity =>
            {
                entity.ToTable("HandshakeResponse");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.ResponseHandshakeResponseRequestId).HasColumnName("Response<HandshakeResponse>_RequestId");
            });

            modelBuilder.Entity<IdentificationChannel>(entity =>
            {
                entity.ToTable("IdentificationChannel");

                entity.HasIndex(e => e.DefaultIdentifierEntityId, "IX_IdentificationChannel_DefaultIdentifierEntityId");

                entity.HasIndex(e => e.DefaultIdentifierPropertyId, "IX_IdentificationChannel_DefaultIdentifierPropertyId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.DefaultIdentifierEntity).WithMany(p => p.IdentificationChannels).HasForeignKey(d => d.DefaultIdentifierEntityId);

            });

            modelBuilder.Entity<LoginRequest>(entity =>
            {
                entity.ToTable("LoginRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestLoginRequestDateTime).HasColumnName("Request<LoginRequest>_DateTime");
            });

            modelBuilder.Entity<MatchingCustomer>(entity =>
            {
                entity.ToTable("MatchingCustomer");

                entity.HasIndex(e => e.SearchCustomerResponseId, "IX_MatchingCustomer_SearchCustomerResponseId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.SearchCustomerResponse).WithMany(p => p.MatchingCustomers).HasForeignKey(d => d.SearchCustomerResponseId);
            });

          

            modelBuilder.Entity<RegisterRequest>(entity =>
            {
                entity.ToTable("RegisterRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.RequestRegisterRequestDateTime).HasColumnName("Request<RegisterRequest>_DateTime");
            });

            modelBuilder.Entity<RegisterResponse>(entity =>
            {
                entity.ToTable("RegisterResponse");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.ResponseRegisterResponseDateTime).HasColumnName("Response<RegisterResponse>_DateTime");
                entity.Property(e => e.ResponseRegisterResponseRequestId).HasColumnName("Response<RegisterResponse>_RequestId");
            });

            modelBuilder.Entity<ResetCustomerChannelPasswordRequest>(entity =>
            {
                entity.ToTable("ResetCustomerChannelPasswordRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.ResetCustomerChannelPasswordRequestDateTime).HasColumnName("ResetCustomerChannelPasswordRequest_DateTime");
            });

            modelBuilder.Entity<ResponseCreateCustomerEntityInstanceResponse>(entity =>
            {
                entity.ToTable("Response<CreateCustomerEntityInstanceResponse>");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
            });

            //modelBuilder.Entity<ResponseCreateEntityCategoryResponse>(entity =>
            //{
            //    entity.ToTable("Response<CreateEntityCategoryResponse>");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<ResponseCreateInfoPropertyResponse>(entity =>
            //{
            //    entity.ToTable("Response<CreateInfoPropertyResponse>");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<ResponseGetCustomerInfoResponse>(entity =>
            //{
            //    entity.ToTable("Response<GetCustomerInfoResponse>");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<ResponseGetEntityCategoriesResponse>(entity =>
            //{
            //    entity.ToTable("Response<GetEntityCategoriesResponse>");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            //modelBuilder.Entity<ResponseGetEntityPropertiesResponse>(entity =>
            //{
            //    entity.ToTable("Response<GetEntityPropertiesResponse>");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EncryptedPayload).IsRequired();
            //    entity.Property(e => e.Iv)
            //        .IsRequired()
            //        .HasColumnName("IV");
            //});

            modelBuilder.Entity<ResponseGetInfoPropertiesResponse>(entity =>
            {
                entity.ToTable("Response<GetInfoPropertiesResponse>");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
            });

            modelBuilder.Entity<ResponseSearchCustomerResponse>(entity =>
            {
                entity.ToTable("Response<SearchCustomerResponse>");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
            });

           

        

            modelBuilder.Entity<SearchCustomerByAnyValueRequest>(entity =>
            {
                entity.ToTable("SearchCustomerByAnyValueRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.SearchCustomerByAnyValueRequestDateTime).HasColumnName("SearchCustomerByAnyValueRequest_DateTime");
            });

            modelBuilder.Entity<SearchCustomerByPropertyValueRequest>(entity =>
            {
                entity.ToTable("SearchCustomerByPropertyValueRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.SearchCustomerByPropertyValueRequestDateTime).HasColumnName("SearchCustomerByPropertyValueRequest_DateTime");
            });

          

            //modelBuilder.Entity<SearchCustomerResponse1>(entity =>
            //{
            //    entity.ToTable("SearchCustomerResponse1");

            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.SearchCustomerResponse1).HasForeignKey<SearchCustomerResponse1>(d => d.Id);
            //});

            modelBuilder.Entity<Session>(entity =>
            {
                entity.ToTable("Session");

                entity.HasIndex(e => e.IdentificationChannelId, "IX_Session_IdentificationChannelId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.IdentificationChannel).WithMany(p => p.Sessions)
                    .HasForeignKey(d => d.IdentificationChannelId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.ToTable("SystemSetting");

                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<UpdateCustomerChannelPasswordRequest>(entity =>
            {
                entity.ToTable("UpdateCustomerChannelPasswordRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.UpdateCustomerChannelPasswordRequestDateTime).HasColumnName("UpdateCustomerChannelPasswordRequest_DateTime");
            });

            //modelBuilder.Entity<UpdateCustomerEntityInstanceRequest>(entity =>
            //{
            //    entity.ToTable("UpdateCustomerEntityInstanceRequest");

            //    entity.Property(e => e.Id).ValueGeneratedNever();
            //    entity.Property(e => e.EntityInstanceName).IsRequired();

            //    entity.HasOne(d => d.IdNavigation).WithOne(p => p.UpdateCustomerEntityInstanceRequest).HasForeignKey<UpdateCustomerEntityInstanceRequest>(d => d.Id);
            //});

            modelBuilder.Entity<UpdateCustomerEntityRequest>(entity =>
            {
                entity.ToTable("UpdateCustomerEntityRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
            });

            modelBuilder.Entity<UpdateCustomerInfoRequest>(entity =>
            {
                entity.ToTable("UpdateCustomerInfoRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.UpdateCustomerInfoRequestDateTime).HasColumnName("UpdateCustomerInfoRequest_DateTime");
            });

            modelBuilder.Entity<UpdateEntityCategoryRequest>(entity =>
            {
                entity.ToTable("UpdateEntityCategoryRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.UpdateEntityCategoryRequestDateTime).HasColumnName("UpdateEntityCategoryRequest_DateTime");
            });

            modelBuilder.Entity<UpdateInfoPropertyRequest>(entity =>
            {
                entity.ToTable("UpdateInfoPropertyRequest");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.EncryptedPayload).IsRequired();
                entity.Property(e => e.Iv)
                    .IsRequired()
                    .HasColumnName("IV");
                entity.Property(e => e.UpdateInfoPropertyRequestDateTime).HasColumnName("UpdateInfoPropertyRequest_DateTime");
            });

           
    

           
        }
    }
}