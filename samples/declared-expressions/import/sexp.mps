<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="8280c166-ac26-4616-84f6-f7ab10abb1e8" name="import">
  <nodes>
    <node id="d6d50b1e-05ef-4c62-871f-02b5c20ad403" concept="Import.Importer" name="sexp">
      <properties>
        <name>sexp</name>
      </properties>
      <children role="tokens">
        <node id="f0a30b9a-52e9-430f-856f-0cbc4d6ab165" concept="Import.Skipped" name="Space">
          <properties>
            <tokenName>Space</tokenName>
            <prefix></prefix>
            <until>\n</until>
          </properties>
        </node>
        <node id="1e3c668c-5549-40cf-8747-5d4605abe071" concept="Import.Number" name="Number">
          <properties>
            <tokenName>Number</tokenName>
          </properties>
        </node>
        <node id="caa2a00f-e735-4fb2-a84e-c2f5b02ca823" concept="Import.Word" name="Word">
          <properties>
            <tokenName>Word</tokenName>
          </properties>
        </node>
        <node id="303de556-a1f6-4386-8d29-9937377e472d" concept="Import.Word" name="Operator">
          <properties>
            <tokenName>Operator</tokenName>
            <extra>+-*/&lt;&gt;=!</extra>
          </properties>
        </node>
      </children>
      <children role="rules">
        <node id="1636cd96-7840-432e-b89f-a94e1b627435" concept="Import.Rule" name="Decl.Int">
          <properties>
            <conceptKey>Decl.Int</conceptKey>
          </properties>
          <children role="parts">
            <node id="fd61dbfe-ffa1-494b-99fd-1aa886fb6f77" concept="Import.Literal" name="(int ">
              <properties>
                <text>(int </text>
              </properties>
            </node>
            <node id="29913039-2d24-4bea-8d50-c1656fa576ec" concept="Import.Capture" name="value">
              <properties>
                <slot>value</slot>
                <token>Number</token>
              </properties>
            </node>
            <node id="003214b1-767c-4fdf-b83b-e145d011852b" concept="Import.Literal" name=")">
              <properties>
                <text>)</text>
              </properties>
            </node>
          </children>
        </node>
        <node id="96831ee4-1c9d-46a1-ac17-c755396df0f9" concept="Import.Rule" name="Decl.Bool">
          <properties>
            <conceptKey>Decl.Bool</conceptKey>
          </properties>
          <children role="parts">
            <node id="2278ed61-ba63-4d46-89dd-285cef70fe74" concept="Import.Literal" name="(bool ">
              <properties>
                <text>(bool </text>
              </properties>
            </node>
            <node id="0590795d-35ea-4ed6-b9c5-cc8b98b204d1" concept="Import.Capture" name="value">
              <properties>
                <slot>value</slot>
                <token>Word</token>
              </properties>
            </node>
            <node id="db606be4-6529-4e2c-bebe-e7be05536d52" concept="Import.Literal" name=")">
              <properties>
                <text>)</text>
              </properties>
            </node>
          </children>
        </node>
        <node id="6cef665a-0798-4c05-8e31-f8d32457d5d9" concept="Import.Rule" name="Decl.Binary">
          <properties>
            <conceptKey>Decl.Binary</conceptKey>
          </properties>
          <children role="parts">
            <node id="c8f50faa-14b6-4327-861f-8f08cd983a01" concept="Import.Literal" name="(binary ">
              <properties>
                <text>(binary </text>
              </properties>
            </node>
            <node id="b14eee69-ad2f-4073-b6af-e5ca60ed648a" concept="Import.Capture" name="operator">
              <properties>
                <slot>operator</slot>
                <token>Operator</token>
              </properties>
            </node>
            <node id="9aab1155-2e8f-4df7-8cce-046ca009663b" concept="Import.Nest" name="left">
              <properties>
                <slot>left</slot>
              </properties>
            </node>
            <node id="dbc2e24a-0843-4d33-bc1f-656d93eac6d3" concept="Import.Nest" name="right">
              <properties>
                <slot>right</slot>
              </properties>
            </node>
            <node id="6d99d0b2-5820-4056-b6eb-3dcfe38d44ea" concept="Import.Literal" name=")">
              <properties>
                <text>)</text>
              </properties>
            </node>
          </children>
        </node>
        <node id="82fcf2f7-c16c-45d8-97d5-7241c32e0dfd" concept="Import.Rule" name="Decl.If">
          <properties>
            <conceptKey>Decl.If</conceptKey>
          </properties>
          <children role="parts">
            <node id="c573f8d1-386e-4d71-af42-e03e0318c5ec" concept="Import.Literal" name="(if ">
              <properties>
                <text>(if </text>
              </properties>
            </node>
            <node id="1fb1034c-39f6-4171-8f6c-31bf95c70b81" concept="Import.Nest" name="condition">
              <properties>
                <slot>condition</slot>
              </properties>
            </node>
            <node id="c719e1d0-422e-472a-a67d-d533b9056241" concept="Import.Nest" name="then">
              <properties>
                <slot>then</slot>
              </properties>
            </node>
            <node id="7855cd83-1916-4dee-b354-5e668c5e03c7" concept="Import.Nest" name="else">
              <properties>
                <slot>else</slot>
              </properties>
            </node>
            <node id="799e4490-e179-442c-a6e8-03f73f541692" concept="Import.Literal" name=")">
              <properties>
                <text>)</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>