<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="153b1c8b-c032-4656-9a28-da668222a746" name="import">
  <nodes>
    <node id="2f2933c7-de35-40a2-ab15-6580912094c1" concept="Import.Importer" name="relaxed">
      <properties>
        <name>relaxed</name>
      </properties>
      <children role="tokens">
        <node id="aa5ab0cc-9cfe-4775-8ccd-e44c53e3476d" concept="Import.Quoted" name="Text">
          <properties>
            <tokenName>Text</tokenName>
            <quote>"</quote>
            <escape>\</escape>
          </properties>
        </node>
        <node id="b98aa3a5-8140-46b4-a4b4-574f4a78b53e" concept="Import.Skipped" name="Space">
          <properties>
            <tokenName>Space</tokenName>
            <prefix></prefix>
            <until>\n</until>
          </properties>
        </node>
        <node id="f0cfab98-f2ec-4af5-96a4-b8fffb593a0d" concept="Import.Skipped" name="Comment">
          <properties>
            <tokenName>Comment</tokenName>
            <prefix>#</prefix>
            <until>\n</until>
          </properties>
        </node>
      </children>
      <children role="rules">
        <node id="036a3530-ba5c-4499-a16d-ec2aeffcc32a" concept="Import.Rule" name="Pipeline.Pipeline">
          <properties>
            <conceptKey>Pipeline.Pipeline</conceptKey>
          </properties>
          <children role="parts">
            <node id="ef8bb6a2-8401-4701-827e-f08c780bf414" concept="Import.Literal" name="pipeline">
              <properties>
                <text>pipeline</text>
              </properties>
            </node>
            <node id="37524429-77f2-43bd-bf18-0d73756d6cc8" concept="Import.Literal" name="=">
              <properties>
                <text>=</text>
              </properties>
            </node>
            <node id="ee876a7a-e93f-422d-9b29-cf4f7d21bd09" concept="Import.Capture" name="name">
              <properties>
                <slot>name</slot>
                <token>Text</token>
              </properties>
            </node>
            <node id="d576d688-76b9-493c-ba61-e07512b049f5" concept="Import.Repeat" name="stages">
              <properties>
                <slot>stages</slot>
                <separator></separator>
                <minimum>0</minimum>
              </properties>
            </node>
          </children>
        </node>
        <node id="ed3d5e6b-a960-4622-95ef-2006f11b03cf" concept="Import.Rule" name="Pipeline.Stage">
          <properties>
            <conceptKey>Pipeline.Stage</conceptKey>
          </properties>
          <children role="parts">
            <node id="54345bd1-6b9f-474e-abc5-1752cf878b22" concept="Import.Literal" name="stage">
              <properties>
                <text>stage</text>
              </properties>
            </node>
            <node id="c2a56b19-9c11-4676-b74b-beaf19dbdccb" concept="Import.Literal" name="=">
              <properties>
                <text>=</text>
              </properties>
            </node>
            <node id="d44c3ec5-f716-4485-ad77-a984935251d9" concept="Import.Capture" name="command">
              <properties>
                <slot>command</slot>
                <token>Text</token>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>