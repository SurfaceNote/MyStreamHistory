﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyStreamHistory.API.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyStreamHistory.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250201220346_Migration_20250201_230342")]
    partial class Migration_20250201_230342
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MyStreamHistory.API.Models.Streamer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AccessToken")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<int>("BroadcasterType")
                        .HasColumnType("integer");

                    b.Property<string>("ChannelName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool?>("FreshToken")
                        .HasColumnType("boolean");

                    b.Property<string>("LogoUser")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("RefreshToken")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<int>("TwitchId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("TwitchId")
                        .IsUnique();

                    b.ToTable("Streamers");
                });
#pragma warning restore 612, 618
        }
    }
}
