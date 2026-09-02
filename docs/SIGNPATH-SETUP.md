# Configuração do SignPath.io (Assinatura de Código Open-Source Gratuita)

O **SignPath.io** oferece certificados de assinatura de código gratuitos para projetos open-source através da **SignPath Foundation**. A assinatura digital elimina alertas do Windows SmartScreen e bloqueios de antivírus.

---

## Passo 1: Cadastro no SignPath

1. Acesse: [https://about.signpath.io/open-source](https://about.signpath.io/open-source)
2. Clique em **"Apply for free open source code signing"**.
3. Crie sua conta e informe o link do repositório no GitHub: `https://github.com/AlissonAz/compress-my-web`.
4. A equipe da SignPath Foundation analisa e aprova o projeto (geralmente em 1 a 2 dias úteis).

---

## Passo 2: Configuração no Painel do SignPath

Após a aprovação:
1. No painel do SignPath, você terá:
   * **Organization ID** (ID da sua organização no SignPath).
   * **Project Slug** (exemplo: `compress-my-web`).
   * **Signing Policy Slug** (exemplo: `release-signing` ou `test-signing`).
2. Gere um **API Token** no painel da sua conta.

---

## Passo 3: Configurar Segredos no GitHub

No seu repositório no GitHub (`github.com/AlissonAz/compress-my-web`):

1. Vá em **Settings** > **Secrets and variables** > **Actions**.
2. Na aba **Secrets** (Segredos):
   * Crie o segredo `SIGNPATH_API_TOKEN` com o token gerado no SignPath.
3. Na aba **Variables** (Variáveis):
   * Crie a variável `SIGNPATH_ORGANIZATION_ID` com o ID da sua organização.
   * Crie a variável `SIGNPATH_PROJECT_SLUG` com `compress-my-web`.
   * Crie a variável `SIGNPATH_POLICY_SLUG` com `release-signing`.

---

## Passo 4: Como Funciona a Publicação Automática

Quando você criar uma nova tag de versão e enviá-la ao GitHub:

```bash
git tag v1.6.2
git push origin v1.6.2
```

O workflow do GitHub Actions em `.github/workflows/release.yml` irá:
1. Compilar o pacote Linux (`.deb`).
2. Compilar o Instalador Windows (`.exe`) e o pacote portátil (`.zip`).
3. Enviar o `.exe` para o SignPath.io assinar digitalmente.
4. Anexar exatamente esses três arquivos diretamente na aba **Releases** do repositório.
