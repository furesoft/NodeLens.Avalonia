<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="46ac0058-40a7-4767-8c5f-10c992af0505" name="workflow">
  <nodes>
    <node id="e4f0ae93-d9a1-4e9e-bcb0-68af7f39c1ae" concept="Evaluation.Rule" name="Workflow.Workflow">
      <properties>
        <conceptKey>Workflow.Workflow</conceptKey>
      </properties>
      <children role="body">
        <node id="e769938e-5bd2-4e39-b301-85f8d15bf7a4" concept="Evaluation.First" name="library">
          <properties>
            <over>library</over>
          </properties>
          <children role="when">
            <node id="6f35a752-d8ca-437b-a448-c9efc436f4c9" concept="Evaluation.Has" name="command">
              <properties>
                <slot>command</slot>
              </properties>
            </node>
          </children>
          <children role="then">
            <node id="1bb796e9-56cf-411a-8089-8f12c8b60390" concept="Evaluation.Property" name="command">
              <properties>
                <slot>command</slot>
              </properties>
            </node>
          </children>
          <children role="otherwise">
            <node id="de4149b3-fe6e-4d90-959c-42fd4213e05b" concept="Evaluation.Fail" name="no step in the library runs a command, so there is nothing to do.">
              <properties>
                <message>no step in the library runs a command, so there is nothing to do.</message>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>