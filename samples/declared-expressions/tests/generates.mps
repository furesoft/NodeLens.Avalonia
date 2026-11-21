<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9005-000000000005" name="generates">
  <nodes>
    <node id="d0000000-0000-4000-8005-000000000005" concept="Testing.Test" name="expressions.mps">
      <properties>
        <document>expressions.mps</document>
      </properties>
      <children role="assertion">
        <node id="d0000000-0000-4000-8005-000000000501" concept="Testing.Generates">
          <properties>
            <generator>Sexpr</generator>
          </properties>
          <children role="lines">
            <node id="d0000000-0000-4000-8005-000000000502" concept="Testing.Line" name="(if (binary &lt; (int 1) (int 2)) (int 3) (int 4))">
              <properties>
                <text>(if (binary &lt; (int 1) (int 2)) (int 3) (int 4))</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>