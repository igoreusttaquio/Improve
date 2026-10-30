"""Gera os .resx de pt-BR (neutro), en e es do app e do Core a partir das tabelas abaixo.

As tabelas são a fonte das traduções: edite aqui e rode, na raiz do repositório,
    python tools/gen-strings/gen-strings.py
"""
import os
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
"""

# chave: (pt-BR, en, es)
APP = {
    "AppTagline": ("Selecione. Escolha. Melhore.", "Select. Choose. Improve.", "Selecciona. Elige. Mejora."),
    "TrayOpenSettings": ("Abrir configurações", "Open settings", "Abrir configuración"),
    "TrayPause": ("Pausar improve", "Pause improve", "Pausar improve"),
    "TrayResume": ("Retomar improve", "Resume improve", "Reanudar improve"),
    "TrayQuit": ("Sair", "Quit", "Salir"),
    "SettingsTitle": ("Configurações do improve", "improve settings", "Configuración de improve"),
    "NavExtensions": ("Extensões", "Extensions", "Extensiones"),
    "NavProviders": ("Provedores", "Providers", "Proveedores"),
    "NavGeneral": ("Geral", "General", "General"),
    "NavAbout": ("Sobre", "About", "Acerca de"),
    "Save": ("Salvar", "Save", "Guardar"),
    "Cancel": ("Cancelar", "Cancel", "Cancelar"),
    "Remove": ("Remover", "Remove", "Quitar"),
    "Delete": ("Excluir", "Delete", "Eliminar"),
    "Back": ("Voltar", "Back", "Volver"),
    "ModeReplace": ("Substituir", "Replace", "Reemplazar"),
    "ModeSuggest": ("Sugerir", "Suggest", "Sugerir"),
    "ModeCopy": ("Copiar", "Copy", "Copiar"),
    "ExtensionsNew": ("Nova extensão", "New extension", "Nueva extensión"),
    "ExtensionsImport": ("Importar", "Import", "Importar"),
    "ExtensionsExport": ("Exportar", "Export", "Exportar"),
    "ExtensionsEmpty": ("Nenhuma extensão ainda.", "No extensions yet.", "Todavía no hay extensiones."),
    "ExtensionsEmptyAction": ("Criar primeira extensão", "Create first extension", "Crear la primera extensión"),
    "ExtensionsHint": ("Arraste para reordenar. A ordem é a do popup; as 9 primeiras ativas ganham atalho de 1 a 9.",
                       "Drag to reorder. This is the popup order; the first 9 active ones get shortcuts 1 to 9.",
                       "Arrastra para reordenar. Es el orden del popup; las 9 primeras activas tienen atajo del 1 al 9."),
    "CardPopupKey": ("atalho {0}", "shortcut {0}", "atajo {0}"),
    "CardNoProvider": ("Sem provedor", "No provider", "Sin proveedor"),
    "ImportPending": ("{0} extensões prontas para importar. Escolha o provedor:", "{0} extensions ready to import. Choose the provider:",
                      "{0} extensiones listas para importar. Elige el proveedor:"),
    "ImportConfirm": ("Importar", "Import", "Importar"),
    "ExportFileName": ("extensoes-improve.json", "improve-extensions.json", "extensiones-improve.json"),
    "JsonFileType": ("Extensões do improve (JSON)", "improve extensions (JSON)", "Extensiones de improve (JSON)"),
    "EditorNewTitle": ("Nova extensão", "New extension", "Nueva extensión"),
    "EditorEditTitle": ("Editar extensão", "Edit extension", "Editar extensión"),
    "FieldName": ("Nome", "Name", "Nombre"),
    "FieldIcon": ("Ícone", "Icon", "Icono"),
    "FieldPrompt": ("Prompt", "Prompt", "Prompt"),
    "PromptMarkdownHint": ("Aceita Markdown. As variáveis ficam destacadas; clique para inserir no cursor:",
                           "Markdown supported. Variables are highlighted; click to insert at the cursor:",
                           "Admite Markdown. Las variables se resaltan; haz clic para insertarlas en el cursor:"),
    "PromptSelectionNote": ("Sem {{selection}} no prompt, o texto selecionado é enviado à parte.",
                            "Without {{selection}} in the prompt, the selected text is sent separately.",
                            "Sin {{selection}} en el prompt, el texto seleccionado se envía aparte."),
    "VarSelectionTip": ("Texto selecionado", "Selected text", "Texto seleccionado"),
    "VarAppTip": ("Nome do app de origem", "Name of the source app", "Nombre de la app de origen"),
    "VarClipboardTip": ("O que estava na área de transferência", "What was on the clipboard", "Lo que había en el portapapeles"),
    "FieldProvider": ("Provedor", "Provider", "Proveedor"),
    "FieldModel": ("Modelo", "Model", "Modelo"),
    "ModelPlaceholder": ("Escolha na lista ou digite o ID", "Pick from the list or type the ID", "Elige de la lista o escribe el ID"),
    "LoadModels": ("Carregar modelos", "Load models", "Cargar modelos"),
    "ModelsLoaded": ("{0} modelos disponíveis: clique no campo para ver a lista.", "{0} models available: click the field to see the list.",
                     "{0} modelos disponibles: haz clic en el campo para ver la lista."),
    "FieldMode": ("Modo", "Mode", "Modo"),
    "TreatAsCode": ("Tratar resultado como código", "Treat result as code", "Tratar el resultado como código"),
    "TreatAsCodeHint": ("O resultado aparece colorido como num editor de código, no tema claro ou escuro.",
                        "The result is shown with syntax colors, like in a code editor, in light or dark theme.",
                        "El resultado se muestra con colores de sintaxis, como en un editor de código, en tema claro u oscuro."),
    "FieldCodeLanguage": ("Linguagem", "Language", "Lenguaje"),
    "CodeLanguageAuto": ("Automática (pela seleção)", "Automatic (from the selection)", "Automático (por la selección)"),
    "FieldHotkey": ("Atalho próprio (opcional)", "Own shortcut (optional)", "Atajo propio (opcional)"),
    "HotkeyPlaceholder": ("Clique e pressione o atalho", "Click and press the shortcut", "Haz clic y pulsa el atajo"),
    "HotkeyClear": ("Limpar", "Clear", "Borrar"),
    "HotkeyConflict": ("Em uso: {0}.", "In use: {0}.", "En uso: {0}."),
    "HotkeyInvalid": ("Use pelo menos um modificador (Ctrl, Alt, Shift) ou uma tecla F.", "Use at least one modifier (Ctrl, Alt, Shift) or an F key.",
                      "Usa al menos un modificador (Ctrl, Alt, Shift) o una tecla F."),
    "TestTitle": ("Testar", "Test", "Probar"),
    "TestSample": ("Texto de exemplo", "Sample text", "Texto de ejemplo"),
    "TestRun": ("Testar", "Test", "Probar"),
    "TestStop": ("Parar", "Stop", "Detener"),
    "TestSampleDefault": ("oi pessoal, queria avisar q a reuniao de amanha vai atrasar uns 15 min pq to resolvendo um problema aqui",
                          "hey all, just wanted to say tmrw's meeting will start like 15 min late bc im fixing a problem here",
                          "hola a todos, queria avisar q la reunion de mañana va a empezar como 15 min tarde xq estoy arreglando un problema"),
    "NameRequired": ("Dê um nome à extensão.", "Give the extension a name.", "Ponle un nombre a la extensión."),
    "PromptRequired": ("Escreva o prompt.", "Write the prompt.", "Escribe el prompt."),
    "IconSearch": ("Buscar ícone", "Search icon", "Buscar icono"),
    "IconMore": ("Mostrando {0} de {1}. Refine a busca.", "Showing {0} of {1}. Refine the search.", "Mostrando {0} de {1}. Afina la búsqueda."),
    "ProvidersAdd": ("Adicionar provedor", "Add provider", "Añadir proveedor"),
    "ProviderNew": ("Novo provedor", "New provider", "Nuevo proveedor"),
    "FieldKind": ("Tipo", "Type", "Tipo"),
    "KindAnthropic": ("Anthropic", "Anthropic", "Anthropic"),
    "KindOpenAi": ("Compatível com OpenAI (OpenAI, DeepSeek, Ollama)", "OpenAI compatible (OpenAI, DeepSeek, Ollama)",
                   "Compatible con OpenAI (OpenAI, DeepSeek, Ollama)"),
    "FieldBaseUrl": ("URL base", "Base URL", "URL base"),
    "FieldApiKey": ("Chave de API", "API key", "Clave de API"),
    "ApiKeyPlaceholder": ("Cole a chave", "Paste the key", "Pega la clave"),
    "ApiKeyStored": ("Chave salva. Deixe em branco para manter.", "Key saved. Leave blank to keep it.", "Clave guardada. Déjalo en blanco para mantenerla."),
    "ApiKeyMissing": ("Sem chave salva.", "No key saved.", "No hay clave guardada."),
    "TestConnection": ("Testar conexão", "Test connection", "Probar conexión"),
    "TestConnectionOk": ("Conexão ok ({0} ms).", "Connection OK ({0} ms).", "Conexión correcta ({0} ms)."),
    "ProviderNameRequired": ("Dê um nome ao provedor.", "Give the provider a name.", "Ponle un nombre al proveedor."),
    "ProviderUrlInvalid": ("Informe uma URL começando com http:// ou https://.", "Enter a URL starting with http:// or https://.",
                           "Introduce una URL que empiece por http:// o https://."),
    "KeyStorageInsecure": ("Este sistema não tem serviço de segredos: as chaves ficam num arquivo sem criptografia, legível só pelo seu usuário.",
                           "This system has no secret service: keys are stored in an unencrypted file readable only by your user.",
                           "Este sistema no tiene servicio de secretos: las claves se guardan en un archivo sin cifrar, legible solo por tu usuario."),
    "ProviderInUse": ("usado por {0} extensões", "used by {0} extensions", "usado por {0} extensiones"),
    "ProviderKeySaved": ("Chave salva", "Key saved", "Clave guardada"),
    "ProviderKeyMissing": ("Sem chave", "No key", "Sin clave"),
    "FieldGlobalHotkey": ("Atalho global", "Global shortcut", "Atajo global"),
    "GlobalHotkeyHint": ("Abre o popup de extensões sobre o texto selecionado.", "Opens the extensions popup over the selected text.",
                         "Abre el popup de extensiones sobre el texto seleccionado."),
    "FieldTheme": ("Tema", "Theme", "Tema"),
    "ThemeSystem": ("Seguir o sistema", "Follow the system", "Seguir el sistema"),
    "ThemeDark": ("Escuro", "Dark", "Oscuro"),
    "ThemeLight": ("Claro", "Light", "Claro"),
    "FieldLanguage": ("Idioma", "Language", "Idioma"),
    "FieldCodeFontSize": ("Tamanho da fonte do código", "Code font size", "Tamaño de fuente del código"),
    "CodeFontSizeHint": ("Usado no painel de sugestão quando a extensão trata o resultado como código.",
                         "Used in the suggestion panel when the extension treats the result as code.",
                         "Se usa en el panel de sugerencias cuando la extensión trata el resultado como código."),
    "LanguageSystem": ("Seguir o sistema", "Follow the system", "Seguir el sistema"),
    "StartWithSystem": ("Iniciar com o sistema", "Start with the system", "Iniciar con el sistema"),
    "AboutVersion": ("Versão {0}", "Version {0}", "Versión {0}"),
    "AboutLicenses": ("Licenças de terceiros", "Third-party licenses", "Licencias de terceros"),
    "PopupSearch": ("Buscar extensão…", "Search extension…", "Buscar extensión…"),
    "PopupRun": ("executar", "run", "ejecutar"),
    "PopupShortcut": ("atalho", "shortcut", "atajo"),
    "PopupClose": ("fechar", "close", "cerrar"),
    "PopupNoMatches": ("Nenhuma extensão encontrada.", "No extension found.", "No se encontró ninguna extensión."),
    "PopupProcessing": ("Processando…", "Processing…", "Procesando…"),
    "PopupEscCancel": ("Esc cancela", "Esc cancels", "Esc cancela"),
    "PopupWaiting": ("Gerando resposta…", "Generating response…", "Generando respuesta…"),
    "Apply": ("Aplicar", "Apply", "Aplicar"),
    "Copy": ("Copiar", "Copy", "Copiar"),
    "Retry": ("Tentar de novo", "Try again", "Reintentar"),
    "Refine": ("Refinar", "Refine", "Refinar"),
    "BackToExtensions": ("Voltar para as extensões", "Back to extensions", "Volver a las extensiones"),
    "Close": ("Fechar", "Close", "Cerrar"),
    "RefinePlaceholder": ("Refinar: mais curto, mais formal…", "Refine: shorter, more formal…", "Refinar: más corto, más formal…"),
    "WindowChanged": ("A janela mudou; use Copiar ou Aplicar.", "The window changed; use Copy or Apply.", "La ventana cambió; usa Copiar o Aplicar."),
    "OpenSettings": ("Abrir configurações", "Open settings", "Abrir configuración"),
    "SelectTextFirst": ("Selecione um texto primeiro", "Select some text first", "Selecciona un texto primero"),
    "Copied": ("Copiado", "Copied", "Copiado"),
    "AboutPrivacy": ("Sem telemetria. O texto selecionado só sai da máquina para o provedor que você configurou.",
                     "No telemetry. Selected text only leaves your machine to the provider you configured.",
                     "Sin telemetría. El texto seleccionado solo sale de tu equipo hacia el proveedor que configuraste."),
}

CORE = {
    "SettingsNewerVersion": ("{0} foi criado por uma versão mais nova do improve. Atualize o app para continuar.",
                             "{0} was created by a newer version of improve. Update the app to continue.",
                             "{0} lo creó una versión más nueva de improve. Actualiza la app para continuar."),
    "ErrorInvalidKey": ("Chave de API inválida para {0}. Abra Configurações > Provedores.", "Invalid API key for {0}. Open Settings > Providers.",
                        "Clave de API no válida para {0}. Abre Configuración > Proveedores."),
    "ErrorMissingKey": ("Nenhuma chave de API configurada para {0}. Abra Configurações > Provedores.", "No API key set for {0}. Open Settings > Providers.",
                        "No hay clave de API configurada para {0}. Abre Configuración > Proveedores."),
    "ErrorRateLimited": ("Limite de uso atingido em {0}. Tente de novo em instantes.", "Usage limit reached on {0}. Try again in a moment.",
                         "Límite de uso alcanzado en {0}. Vuelve a intentarlo en un momento."),
    "ErrorUnavailable": ("{0} está indisponível no momento. Tente de novo em instantes.", "{0} is unavailable right now. Try again in a moment.",
                         "{0} no está disponible en este momento. Vuelve a intentarlo en un momento."),
    "ErrorNoNetwork": ("Sem conexão com {0}. Verifique sua internet e o endereço do provedor.", "Can't reach {0}. Check your internet and the provider address.",
                       "Sin conexión con {0}. Revisa tu internet y la dirección del proveedor."),
    "ErrorBadRequest": ("{0} recusou o pedido: {1}", "{0} rejected the request: {1}", "{0} rechazó la solicitud: {1}"),
    "ErrorUnknown": ("Erro inesperado em {0}. Tente de novo.", "Unexpected error on {0}. Try again.", "Error inesperado en {0}. Vuelve a intentarlo."),
    "ErrorNoProvider": ("A extensão \"{0}\" não tem provedor configurado. Abra Configurações > Extensões.",
                        "The \"{0}\" extension has no provider. Open Settings > Extensions.",
                        "La extensión \"{0}\" no tiene proveedor. Abre Configuración > Extensiones."),
    "ErrorNoModel": ("Escolha um modelo para a extensão \"{0}\" em Configurações > Extensões.",
                     "Choose a model for the \"{0}\" extension in Settings > Extensions.",
                     "Elige un modelo para la extensión \"{0}\" en Configuración > Extensiones."),
    "GlobalHotkeyOwner": ("atalho global", "global shortcut", "atajo global"),
    "ImportInvalidFile": ("O arquivo não é uma exportação de extensões do improve.", "This file is not an improve extensions export.",
                          "El archivo no es una exportación de extensiones de improve."),
    "ExampleImproveName": ("Melhorar mensagem", "Improve message", "Mejorar mensaje"),
    "ExampleImprovePrompt": ("Reescreva a mensagem para ficar clara, cordial e sem erros, mantendo o idioma, o tom e todas as informações, inclusive saudações.",
                             "Rewrite the message to be clear, friendly and error-free, keeping its language, tone and every piece of information, greetings included.",
                             "Reescribe el mensaje para que sea claro, cordial y sin errores, manteniendo el idioma, el tono y toda la información, saludos incluidos."),
    "ExampleFormalName": ("Deixar formal", "Make formal", "Hacer formal"),
    "ExampleFormalPrompt": ("Reescreva o texto em tom formal e profissional, mantendo o sentido, o idioma e todas as informações. Adapte saudações e expressões informais ao tom formal em vez de removê-las.",
                            "Rewrite the text in a formal, professional tone, keeping its meaning, language and every piece of information. Adapt greetings and casual expressions to the formal tone instead of removing them.",
                            "Reescribe el texto en un tono formal y profesional, manteniendo el sentido, el idioma y toda la información. Adapta los saludos y expresiones informales al tono formal en lugar de eliminarlos."),
    "ExampleTranslateName": ("Traduzir para inglês", "Translate to Spanish", "Traducir al inglés"),
    "ExampleTranslatePrompt": ("Traduza o texto para o inglês, mantendo o tom e a formatação.",
                               "Translate the text into Spanish, keeping its tone and formatting.",
                               "Traduce el texto al inglés, manteniendo el tono y el formato."),
    "ExampleRefactorName": ("Refatorar código", "Refactor code", "Refactorizar código"),
    "ExampleRefactorPrompt": ("Refatore o código para ficar mais legível e idiomático, sem mudar o comportamento. Mantenha a linguagem e o estilo de indentação.",
                              "Refactor the code to be more readable and idiomatic without changing its behavior. Keep the language and indentation style.",
                              "Refactoriza el código para que sea más legible e idiomático, sin cambiar su comportamiento. Mantén el lenguaje y el estilo de sangría."),
}


def write(path, table, index):
    body = "".join(
        f'  <data name="{key}" xml:space="preserve">\n    <value>{escape(values[index])}</value>\n  </data>\n'
        for key, values in table.items())
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(HEADER + body + "</root>\n")


for folder, name, table in ((f"{ROOT}/src/Improve.App/Resources", "Strings", APP), (f"{ROOT}/src/Improve.Core/Resources", "CoreStrings", CORE)):
    for index, suffix in enumerate(("", ".en", ".es")):
        write(f"{folder}/{name}{suffix}.resx", table, index)
print("ok", len(APP), len(CORE))
