﻿// <auto-generated />
using System;
using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("games")
                .HasAnnotation("ProductVersion", "9.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("CampaignManager.Web.Compain.Models.Campaign", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("KeeperEmail")
                        .HasColumnType("text");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text")
                        .HasDefaultValue("Planning");

                    b.HasKey("Id");

                    b.HasIndex("KeeperEmail");

                    b.ToTable("Campaigns", "games");
                });

            modelBuilder.Entity("CampaignManager.Web.Model.CampaignPlayer", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CampaignId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PlayerEmail")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CampaignId");

                    b.HasIndex("PlayerEmail", "CampaignId")
                        .IsUnique();

                    b.ToTable("CampaignPlayers", "games");
                });

            modelBuilder.Entity("CampaignManager.Web.Model.CharacterStorageDto", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid?>("CampaignPlayerId")
                        .HasColumnType("uuid");

                    b.Property<Character>("Character")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasComment("JSON-представление персонажа со всеми характеристиками");

                    b.Property<string>("CharacterName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CampaignPlayerId");

                    b.ToTable("Characters", "games");
                });

            modelBuilder.Entity("CampaignManager.Web.Model.CampaignPlayer", b =>
                {
                    b.HasOne("CampaignManager.Web.Compain.Models.Campaign", "Campaign")
                        .WithMany("Players")
                        .HasForeignKey("CampaignId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Campaign");
                });

            modelBuilder.Entity("CampaignManager.Web.Model.CharacterStorageDto", b =>
                {
                    b.HasOne("CampaignManager.Web.Model.CampaignPlayer", "CampaignPlayer")
                        .WithMany("Characters")
                        .HasForeignKey("CampaignPlayerId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("CampaignPlayer");
                });

            modelBuilder.Entity("CampaignManager.Web.Compain.Models.Campaign", b =>
                {
                    b.Navigation("Players");
                });

            modelBuilder.Entity("CampaignManager.Web.Model.CampaignPlayer", b =>
                {
                    b.Navigation("Characters");
                });
#pragma warning restore 612, 618
        }
    }
}
