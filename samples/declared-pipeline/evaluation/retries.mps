<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="7c000000-0000-4000-9000-000000000001" name="retries">
  <nodes>
    <node id="7c000000-0000-4000-8000-000000000001" concept="Evaluation.Rule" name="Pipeline.Pipeline">
      <properties>
        <conceptKey>Pipeline.Pipeline</conceptKey>
      </properties>
      <children role="body">
        <node id="7c000000-0000-4000-8000-000000000002" concept="Evaluation.Sum" name="stages">
          <properties>
            <over>stages</over>
          </properties>
          <children role="each">
            <node id="7c000000-0000-4000-8000-000000000003" concept="Evaluation.Property" name="retries">
              <properties>
                <slot>retries</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>