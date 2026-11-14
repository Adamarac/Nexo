using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Infrastructure.Messaging.Outbox;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(message => message.Payload).HasColumnType("jsonb");
        builder.HasIndex(message => message.MessageId).IsUnique();

        // Índice só das mensagens pendentes: o envio procura apenas por elas.
        builder.HasIndex(message => message.CreatedAt).HasFilter("sent_at IS NULL").HasDatabaseName("ix_outbox_messages_pending");
    }
}
