<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="8c789c6a-32b7-4854-8021-9f1e15af1114" name="import">
  <nodes>
    <node id="9945c5a5-4d3e-4144-be4d-d3192af17507" concept="Import.Importer" name="ini">
      <properties>
        <name>ini</name>
      </properties>
      <children role="rules">
        <node id="40452451-2fa9-449a-b2ed-de0ec4c04a59" concept="Import.Rule" name="Pipeline.Pipeline">
          <properties>
            <conceptKey>Pipeline.Pipeline</conceptKey>
          </properties>
          <children role="parts">
            <node id="cebc9401-4fc6-4561-a37a-1e55184a4e9e" concept="Import.Literal" name="pipeline = ">
              <properties>
                <text>pipeline = </text>
              </properties>
            </node>
            <node id="65b56afc-f65d-4beb-a0ca-8df2d86afe0b" concept="Import.Capture" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
            <node id="cb6c5407-9e0a-4938-a32e-0a3bded3fe05" concept="Import.Literal" name="&#xA;">
              <properties>
                <text>
</text>
              </properties>
            </node>
            <node id="5c9d426b-4267-45d1-8c16-a9186ae93b2d" concept="Import.Repeat" name="stages">
              <properties>
                <slot>stages</slot>
                <separator>
</separator>
                <minimum>0</minimum>
              </properties>
            </node>
          </children>
        </node>
        <node id="abd28e22-80f0-4931-a2b7-e2291369741a" concept="Import.Rule" name="Pipeline.Stage">
          <properties>
            <conceptKey>Pipeline.Stage</conceptKey>
          </properties>
          <children role="parts">
            <node id="ed067f29-8aff-42e1-9c26-0646651b3c9f" concept="Import.Literal" name="stage = ">
              <properties>
                <text>stage = </text>
              </properties>
            </node>
            <node id="9def3faf-ee8e-41d9-9e01-6fe0dd0a690e" concept="Import.Capture" name="command">
              <properties>
                <slot>command</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>