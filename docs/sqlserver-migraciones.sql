IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [migration_id] nvarchar(150) NOT NULL,
        [product_version] nvarchar(32) NOT NULL,
        CONSTRAINT [pk___ef_migrations_history] PRIMARY KEY ([migration_id])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [usuarios] (
        [id] uniqueidentifier NOT NULL,
        [nombre_usuario] nvarchar(64) NOT NULL,
        [nombre_completo] nvarchar(150) NOT NULL,
        [email] nvarchar(150) NOT NULL,
        [hash_contrasena] nvarchar(max) NOT NULL,
        [activo] bit NOT NULL,
        [ultimo_acceso] datetimeoffset NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_usuarios] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [zonas] (
        [id] uniqueidentifier NOT NULL,
        [nombre] nvarchar(150) NOT NULL,
        [descripcion] nvarchar(1000) NULL,
        [nivel_riesgo] nvarchar(32) NOT NULL,
        [area] geometry NOT NULL,
        [fuente_institucional] nvarchar(150) NULL,
        [referencia_documento] nvarchar(200) NULL,
        [fecha_documento] date NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_zonas] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [avisos] (
        [id] uniqueidentifier NOT NULL,
        [identificador_cap] nvarchar(200) NOT NULL,
        [remitente] nvarchar(200) NOT NULL,
        [enviado] datetimeoffset NOT NULL,
        [formato] nvarchar(32) NOT NULL,
        [estado] nvarchar(32) NOT NULL,
        [tipo_mensaje] nvarchar(32) NOT NULL,
        [estado_cap] nvarchar(32) NOT NULL,
        [evento] nvarchar(200) NOT NULL,
        [urgencia] nvarchar(32) NOT NULL,
        [severidad] nvarchar(32) NOT NULL,
        [certeza] nvarchar(32) NOT NULL,
        [titular] nvarchar(300) NULL,
        [descripcion] nvarchar(max) NULL,
        [instruccion] nvarchar(max) NULL,
        [vigente] datetimeoffset NULL,
        [expira] datetimeoffset NULL,
        [referencias] nvarchar(1000) NULL,
        [cadena_id] uniqueidentifier NULL,
        [descripcion_area] nvarchar(max) NULL,
        [area] geometry NULL,
        [xml_cap] nvarchar(max) NOT NULL,
        [registrado_por_id] uniqueidentifier NOT NULL,
        [contenido_original] nvarchar(max) NULL,
        [tipo_contenido_original] nvarchar(64) NULL,
        [hash_original] nvarchar(64) NULL,
        [incompleto] bit NOT NULL,
        [vencido] bit NOT NULL,
        [apto_para_difusion] bit NOT NULL,
        [motivos_no_apto] nvarchar(max) NOT NULL,
        [aptitud_evaluada_en] datetimeoffset NOT NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_avisos] PRIMARY KEY ([id]),
        CONSTRAINT [fk_avisos_usuarios_registrado_por_id] FOREIGN KEY ([registrado_por_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [contacto_historial] (
        [id] uniqueidentifier NOT NULL,
        [contacto_id] uniqueidentifier NOT NULL,
        [comunidad_id] uniqueidentifier NOT NULL,
        [accion] nvarchar(32) NOT NULL,
        [usuario_id] uniqueidentifier NOT NULL,
        [instantanea] nvarchar(max) NOT NULL,
        [cambios] nvarchar(500) NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_contacto_historial] PRIMARY KEY ([id]),
        CONSTRAINT [CK_contacto_historial_json] CHECK (ISJSON([instantanea]) = 1),
        CONSTRAINT [fk_contacto_historial_usuarios_usuario_id] FOREIGN KEY ([usuario_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [usuario_bitacora] (
        [id] uniqueidentifier NOT NULL,
        [usuario_id] uniqueidentifier NOT NULL,
        [administrador_id] uniqueidentifier NULL,
        [accion] nvarchar(32) NOT NULL,
        [rol] nvarchar(32) NULL,
        [detalle] nvarchar(500) NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_usuario_bitacora] PRIMARY KEY ([id]),
        CONSTRAINT [fk_usuario_bitacora_usuarios_administrador_id] FOREIGN KEY ([administrador_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_usuario_bitacora_usuarios_usuario_id] FOREIGN KEY ([usuario_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [usuario_roles] (
        [rol] nvarchar(32) NOT NULL,
        [usuario_id] uniqueidentifier NOT NULL,
        [asignado_en] datetimeoffset NOT NULL,
        [asignado_por_id] uniqueidentifier NULL,
        CONSTRAINT [pk_usuario_roles] PRIMARY KEY ([usuario_id], [rol]),
        CONSTRAINT [fk_usuario_roles_usuarios_asignado_por_id] FOREIGN KEY ([asignado_por_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_usuario_roles_usuarios_usuario_id] FOREIGN KEY ([usuario_id]) REFERENCES [usuarios] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [comunidades] (
        [id] uniqueidentifier NOT NULL,
        [nombre] nvarchar(150) NOT NULL,
        [municipio] nvarchar(150) NULL,
        [poblacion] int NULL,
        [ubicacion] geometry NULL,
        [zona_id] uniqueidentifier NOT NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_comunidades] PRIMARY KEY ([id]),
        CONSTRAINT [fk_comunidades_zonas_zona_id] FOREIGN KEY ([zona_id]) REFERENCES [zonas] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [versiones_mensaje] (
        [id] uniqueidentifier NOT NULL,
        [aviso_id] uniqueidentifier NOT NULL,
        [cadena_id] uniqueidentifier NOT NULL,
        [numero] int NOT NULL,
        [tipo] nvarchar(32) NOT NULL,
        [estado] nvarchar(32) NOT NULL,
        [estado_cap] nvarchar(32) NOT NULL,
        [area] geometry NULL,
        [identificador_cap] nvarchar(200) NOT NULL,
        [remitente] nvarchar(200) NOT NULL,
        [enviado_cap] datetimeoffset NULL,
        [xml_cap] nvarchar(max) NULL,
        [referencia_version_id] uniqueidentifier NULL,
        [referencias] nvarchar(1000) NULL,
        [version_anterior_id] uniqueidentifier NULL,
        [motivo_devolucion] nvarchar(1000) NULL,
        [preparado_por_id] uniqueidentifier NOT NULL,
        [enviada_a_revision_en] datetimeoffset NULL,
        [revisado_por_id] uniqueidentifier NULL,
        [revisado_en] datetimeoffset NULL,
        [motivo_rechazo] nvarchar(1000) NULL,
        [difundida_en] datetimeoffset NULL,
        [reemplazada_por_id] uniqueidentifier NULL,
        [contenido_certeza] nvarchar(32) NOT NULL,
        [contenido_descripcion] nvarchar(4000) NULL,
        [contenido_descripcion_area] nvarchar(1000) NULL,
        [contenido_evento] nvarchar(200) NOT NULL,
        [contenido_expira] datetimeoffset NULL,
        [contenido_instruccion] nvarchar(4000) NULL,
        [contenido_severidad] nvarchar(32) NOT NULL,
        [contenido_titular] nvarchar(300) NULL,
        [contenido_urgencia] nvarchar(32) NOT NULL,
        [contenido_vigente] datetimeoffset NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_versiones_mensaje] PRIMARY KEY ([id]),
        CONSTRAINT [fk_versiones_mensaje_avisos_aviso_id] FOREIGN KEY ([aviso_id]) REFERENCES [avisos] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_versiones_mensaje_avisos_cadena_id] FOREIGN KEY ([cadena_id]) REFERENCES [avisos] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_versiones_mensaje_usuarios_preparado_por_id] FOREIGN KEY ([preparado_por_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_versiones_mensaje_usuarios_revisado_por_id] FOREIGN KEY ([revisado_por_id]) REFERENCES [usuarios] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_versiones_mensaje_versiones_mensaje_reemplazada_por_id] FOREIGN KEY ([reemplazada_por_id]) REFERENCES [versiones_mensaje] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_versiones_mensaje_versiones_mensaje_referencia_version_id] FOREIGN KEY ([referencia_version_id]) REFERENCES [versiones_mensaje] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_versiones_mensaje_versiones_mensaje_version_anterior_id] FOREIGN KEY ([version_anterior_id]) REFERENCES [versiones_mensaje] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [contactos] (
        [id] uniqueidentifier NOT NULL,
        [comunidad_id] uniqueidentifier NOT NULL,
        [nombre] nvarchar(150) NOT NULL,
        [cargo] nvarchar(100) NULL,
        [activo] bit NOT NULL,
        [procedencia] nvarchar(150) NOT NULL,
        [autorizado] bit NOT NULL,
        [referencia_autorizacion] nvarchar(200) NULL,
        [fecha_autorizacion] date NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_contactos] PRIMARY KEY ([id]),
        CONSTRAINT [fk_contactos_comunidades_comunidad_id] FOREIGN KEY ([comunidad_id]) REFERENCES [comunidades] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [listas_destinos] (
        [id] uniqueidentifier NOT NULL,
        [version_id] uniqueidentifier NOT NULL,
        [generada_por_id] uniqueidentifier NOT NULL,
        [canales] nvarchar(max) NOT NULL,
        [creado_en] datetimeoffset NOT NULL,
        CONSTRAINT [pk_listas_destinos] PRIMARY KEY ([id]),
        CONSTRAINT [fk_listas_destinos_versiones_mensaje_version_id] FOREIGN KEY ([version_id]) REFERENCES [versiones_mensaje] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [contacto_canales] (
        [id] int NOT NULL IDENTITY,
        [tipo] nvarchar(32) NOT NULL,
        [destino] nvarchar(150) NOT NULL,
        [preferido] bit NOT NULL,
        [contacto_id] uniqueidentifier NOT NULL,
        CONSTRAINT [pk_contacto_canales] PRIMARY KEY ([id]),
        CONSTRAINT [fk_contacto_canales_contactos_contacto_id] FOREIGN KEY ([contacto_id]) REFERENCES [contactos] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [comunidades_pendientes] (
        [comunidad_id] uniqueidentifier NOT NULL,
        [lista_destinos_id] uniqueidentifier NOT NULL,
        [comunidad] nvarchar(150) NOT NULL,
        [motivo] nvarchar(300) NOT NULL,
        CONSTRAINT [pk_comunidades_pendientes] PRIMARY KEY ([lista_destinos_id], [comunidad_id]),
        CONSTRAINT [fk_comunidades_pendientes_listas_destinos_lista_destinos_id] FOREIGN KEY ([lista_destinos_id]) REFERENCES [listas_destinos] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE TABLE [destinos_envio] (
        [canal] nvarchar(32) NOT NULL,
        [direccion] nvarchar(150) NOT NULL,
        [lista_destinos_id] uniqueidentifier NOT NULL,
        [comunidad_id] uniqueidentifier NOT NULL,
        [contacto_id] uniqueidentifier NOT NULL,
        CONSTRAINT [pk_destinos_envio] PRIMARY KEY ([lista_destinos_id], [canal], [direccion]),
        CONSTRAINT [fk_destinos_envio_listas_destinos_lista_destinos_id] FOREIGN KEY ([lista_destinos_id]) REFERENCES [listas_destinos] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_avisos_apto_para_difusion] ON [avisos] ([apto_para_difusion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_avisos_cadena_id] ON [avisos] ([cadena_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_avisos_enviado] ON [avisos] ([enviado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_avisos_registrado_por_id] ON [avisos] ([registrado_por_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_avisos_remitente_identificador_cap] ON [avisos] ([remitente], [identificador_cap]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_comunidades_zona_id_nombre] ON [comunidades] ([zona_id], [nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_contacto_canales_contacto_id] ON [contacto_canales] ([contacto_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_contacto_historial_comunidad_id] ON [contacto_historial] ([comunidad_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_contacto_historial_contacto_id_creado_en] ON [contacto_historial] ([contacto_id], [creado_en]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_contacto_historial_usuario_id] ON [contacto_historial] ([usuario_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_contactos_comunidad_id] ON [contactos] ([comunidad_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_destinos_envio_contacto_id] ON [destinos_envio] ([contacto_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_listas_destinos_version_id] ON [listas_destinos] ([version_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_usuario_bitacora_administrador_id] ON [usuario_bitacora] ([administrador_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_usuario_bitacora_usuario_id_creado_en] ON [usuario_bitacora] ([usuario_id], [creado_en]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_usuario_roles_asignado_por_id] ON [usuario_roles] ([asignado_por_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_usuarios_email] ON [usuarios] ([email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_usuarios_nombre_usuario] ON [usuarios] ([nombre_usuario]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_aviso_id] ON [versiones_mensaje] ([aviso_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_versiones_mensaje_cadena_id_numero] ON [versiones_mensaje] ([cadena_id], [numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_estado] ON [versiones_mensaje] ([estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_versiones_mensaje_identificador_cap] ON [versiones_mensaje] ([identificador_cap]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_preparado_por_id] ON [versiones_mensaje] ([preparado_por_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_reemplazada_por_id] ON [versiones_mensaje] ([reemplazada_por_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_referencia_version_id] ON [versiones_mensaje] ([referencia_version_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_revisado_por_id] ON [versiones_mensaje] ([revisado_por_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE INDEX [ix_versiones_mensaje_version_anterior_id] ON [versiones_mensaje] ([version_anterior_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    CREATE UNIQUE INDEX [ix_zonas_nombre] ON [zonas] ([nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261119031546_SqlServerInicial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([migration_id], [product_version])
    VALUES (N'20261119031546_SqlServerInicial', N'10.0.14');
END;

COMMIT;
GO

